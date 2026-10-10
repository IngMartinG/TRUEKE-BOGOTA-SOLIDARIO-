import { expect, type APIRequestContext, type Page } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { deflateSync } from 'node:zlib';
import { URL_MAILPIT } from './ambiente';

export const CLAVE = 'Clave12345';

/** Correo único por prueba (las pruebas corren en paralelo y la base es compartida). */
export function correoUnico(nombre: string): string {
  return `${nombre}-${randomBytes(5).toString('hex')}@trueke.test`;
}

/** PNG de un solo color (sin dependencias): sirve como foto de perfil o de publicación. */
export function fotoPng(ancho = 640, alto = 480, [r, g, b] = [34, 120, 70]): Buffer {
  const crc = (datos: Buffer): number => {
    let c = 0xffffffff;
    for (const byte of datos) {
      c ^= byte;
      for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    }
    return (c ^ 0xffffffff) >>> 0;
  };
  const bloque = (tipo: string, datos: Buffer): Buffer => {
    const largo = Buffer.alloc(4);
    largo.writeUInt32BE(datos.length);
    const cuerpo = Buffer.concat([Buffer.from(tipo, 'ascii'), datos]);
    const control = Buffer.alloc(4);
    control.writeUInt32BE(crc(cuerpo));
    return Buffer.concat([largo, cuerpo, control]);
  };
  const cabecera = Buffer.alloc(13);
  cabecera.writeUInt32BE(ancho, 0);
  cabecera.writeUInt32BE(alto, 4);
  cabecera.set([8, 2, 0, 0, 0], 8); // 8 bits, RGB
  const fila = Buffer.concat([Buffer.from([0]), Buffer.from(Array.from({ length: ancho }, () => [r, g, b]).flat())]);
  const pixeles = deflateSync(Buffer.concat(Array.from({ length: alto }, () => fila)));
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    bloque('IHDR', cabecera),
    bloque('IDAT', pixeles),
    bloque('IEND', Buffer.alloc(0)),
  ]);
}

interface MensajeMailpit {
  ID: string;
  Subject: string;
}

/** Espera el último correo enviado a `para` (por SMTP con TLS a Mailpit) y devuelve su texto. */
export async function esperarCorreo(request: APIRequestContext, para: string, asunto?: RegExp): Promise<string> {
  let texto = '';
  await expect
    .poll(
      async () => {
        const r = await request.get(`${URL_MAILPIT}/api/v1/search`, { params: { query: `to:"${para}"` } });
        const mensajes = ((await r.json()) as { messages: MensajeMailpit[] }).messages ?? [];
        const m = mensajes.find((x) => !asunto || asunto.test(x.Subject));
        if (!m) return false;
        texto = ((await (await request.get(`${URL_MAILPIT}/api/v1/message/${m.ID}`)).json()) as { Text: string }).Text;
        return true;
      },
      { message: `No llegó el correo a ${para}`, timeout: 30_000 },
    )
    .toBe(true);
  return texto;
}

/** Enlace del correo que apunta a una ruta del front, p. ej. "verificar-correo". */
export function enlaceDe(texto: string, ruta: string): string {
  const m = texto.match(new RegExp(`https?://[^\\s"<>]+/${ruta}\\?token=[^\\s"<>]+`));
  if (!m) throw new Error(`El correo no trae el enlace a /${ruta}:\n${texto}`);
  return m[0];
}

export interface Cuenta {
  nombre: string;
  correo: string;
  token: string;
  id: string;
}

/**
 * Cuenta lista para operar, creada por la API (como lo haría el front): registro, verificación con el enlace del
 * correo y foto de perfil subida al almacenamiento con la SAS. Las cookies quedan en el contexto de `page`:
 * al abrir la app, la sesión se restaura sola con la cookie de refresco.
 */
export async function cuentaLista(page: Page, nombre: string, conFoto = true): Promise<Cuenta> {
  const api = page.request;
  const correo = correoUnico(nombre.toLowerCase());
  const registro = await api.post('/api/v1/auth/registrar', {
    data: { nombreCompleto: `${nombre} Pruebas`, municipioCodigo: '11001', localidad: 'Chapinero', correo, clave: CLAVE, aceptoPoliticaDatos: true },
  });
  expect(registro.status(), await registro.text()).toBe(201);
  const sesion = (await registro.json()) as { token: string; usuario: { id: string } };

  const enlace = enlaceDe(await esperarCorreo(api, correo, /confirma|verifica/i), 'verificar-correo');
  const token = new URL(enlace).searchParams.get('token');
  expect((await api.post('/api/v1/auth/verificar-correo', { data: { token } })).ok()).toBe(true);

  const cuenta: Cuenta = { nombre, correo, token: sesion.token, id: sesion.usuario.id };
  if (conFoto) {
    const url = await subirFoto(api, cuenta.token, fotoPng(400, 400, [90, 60, 140]));
    const r = await api.put('/api/v1/usuarios/yo/foto', { data: { url }, headers: { Authorization: `Bearer ${cuenta.token}` } });
    expect(r.ok(), await r.text()).toBe(true);
  }
  return cuenta;
}

/** Ingresa por la pantalla de ingreso, como una persona (la cookie de refresco la guarda el navegador). */
export async function ingresar(page: Page, cuenta: Cuenta): Promise<void> {
  await page.goto('/ingresar');
  await page.getByLabel('Correo electrónico').fill(cuenta.correo);
  await page.getByLabel('Contraseña', { exact: true }).fill(CLAVE);
  await page.getByRole('button', { name: 'Ingresar', exact: true }).click();
  await expect(page).not.toHaveURL(/\/ingresar/);
}

/** Sube una imagen como lo hace el navegador: pide la SAS a la API y hace PUT directo al almacenamiento. */
export async function subirFoto(api: APIRequestContext, token: string, png: Buffer): Promise<string> {
  const r = await api.post('/api/v1/archivos/subidas', {
    data: { tipo: 'Imagen', contentType: 'image/png', tamanoBytes: png.length },
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(r.status(), await r.text()).toBe(201);
  const subida = (await r.json()) as { urlSubida: string; urlArchivo: string; cabeceras: Record<string, string> };
  const put = await api.put(subida.urlSubida, { data: png, headers: subida.cabeceras });
  expect(put.status(), await put.text()).toBe(201);
  return subida.urlArchivo;
}

/** Publicación con foto creada por la API (para las pruebas que empiezan con algo ya publicado). */
export async function publicar(api: APIRequestContext, cuenta: Cuenta, titulo: string, modo: 'Trueke' | 'Compra' | 'Donacion' = 'Trueke'): Promise<string> {
  const foto = await subirFoto(api, cuenta.token, fotoPng());
  const r = await api.post('/api/v1/publicaciones', {
    headers: { Authorization: `Bearer ${cuenta.token}` },
    data: {
      titulo, descripcion: 'Publicada por las pruebas de punta a punta.', categoriaId: 4, modo, condicion: 'Usado',
      municipioCodigo: '11001', localidad: 'Chapinero', precioReferenciaCop: modo === 'Compra' ? 50000 : null, imagenes: [foto],
    },
  });
  expect(r.status(), await r.text()).toBe(201);
  return ((await r.json()) as { id: string }).id;
}
