import { configurarCdn, miniatura, urlPublica } from './imagenes';

const USUARIO = '0123456789abcdef0123456789abcdef';
const FOTO = 'fedcba9876543210fedcba9876543210';
const BLOB = 'https://cuenta.blob.core.windows.net/';

describe('imagenes', () => {
  afterEach(() => configurarCdn(null, null));

  it('deriva la miniatura de las fotos de la plataforma y deja igual las demás', () => {
    expect(miniatura(`${BLOB}imagenes/${USUARIO}/${FOTO}.jpg`)).toBe(`${BLOB}imagenes/${USUARIO}/${FOTO}.min.webp`);
    expect(miniatura(`${BLOB}imagenes/${USUARIO}/${FOTO}.png`)).toBe(`${BLOB}imagenes/${USUARIO}/${FOTO}.min.webp`);
    expect(miniatura('https://ejemplo.co/foto.jpg')).toBe('https://ejemplo.co/foto.jpg');
    expect(miniatura(`${BLOB}imagenes/${USUARIO}/${FOTO}.min.webp`)).toBe(`${BLOB}imagenes/${USUARIO}/${FOTO}.min.webp`);
    expect(miniatura(null)).toBeNull();
  });

  it('con CDN descarga las fotos de Blob desde la CDN', () => {
    const url = `${BLOB}imagenes/${USUARIO}/${FOTO}.jpg`;
    expect(urlPublica(url)).toBe(url);

    configurarCdn(BLOB, 'https://fotos.trueke.co/');
    expect(urlPublica(url)).toBe(`https://fotos.trueke.co/imagenes/${USUARIO}/${FOTO}.jpg`);
    expect(urlPublica('https://otra.example/x.jpg')).toBe('https://otra.example/x.jpg');
  });

  it('en desarrollo reescribe Azurite a una ruta del mismo sitio', () => {
    expect(urlPublica('http://127.0.0.1:10000/devstoreaccount1/imagenes/x.jpg')).toBe('/devstoreaccount1/imagenes/x.jpg');
  });
});
