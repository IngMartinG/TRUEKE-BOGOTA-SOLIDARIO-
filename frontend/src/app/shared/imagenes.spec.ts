import { configurarCdn, miniatura, urlPublica } from './imagenes';

const USUARIO = '0123456789abcdef0123456789abcdef';
const FOTO = 'fedcba9876543210fedcba9876543210';
const PUBLICO = 'https://proyecto.supabase.co/storage/v1/object/public/';

describe('imagenes', () => {
  afterEach(() => configurarCdn(null, null));

  it('deriva la miniatura de las fotos de la plataforma y deja igual las demás', () => {
    expect(miniatura(`${PUBLICO}imagenes/${USUARIO}/${FOTO}.jpg`)).toBe(`${PUBLICO}imagenes/${USUARIO}/${FOTO}.min.webp`);
    expect(miniatura(`${PUBLICO}imagenes/${USUARIO}/${FOTO}.png`)).toBe(`${PUBLICO}imagenes/${USUARIO}/${FOTO}.min.webp`);
    expect(miniatura('https://ejemplo.co/foto.jpg')).toBe('https://ejemplo.co/foto.jpg');
    expect(miniatura(`${PUBLICO}imagenes/${USUARIO}/${FOTO}.min.webp`)).toBe(`${PUBLICO}imagenes/${USUARIO}/${FOTO}.min.webp`);
    expect(miniatura(null)).toBeNull();
  });

  it('con CDN descarga las fotos de Storage desde la CDN', () => {
    const url = `${PUBLICO}imagenes/${USUARIO}/${FOTO}.jpg`;
    expect(urlPublica(url)).toBe(url);

    configurarCdn(PUBLICO, 'https://fotos.trueke.co/');
    expect(urlPublica(url)).toBe(`https://fotos.trueke.co/imagenes/${USUARIO}/${FOTO}.jpg`);
    expect(urlPublica('https://otra.example/x.jpg')).toBe('https://otra.example/x.jpg');
  });

  it('en desarrollo reescribe el Storage local a una ruta del mismo sitio', () => {
    expect(urlPublica('http://127.0.0.1:8082/storage/v1/object/public/imagenes/x.jpg')).toBe('/storage/v1/object/public/imagenes/x.jpg');
    expect(urlPublica('http://localhost:8082/storage/v1/object/upload/sign/subidas/x.jpg?token=t')).toBe(
      '/storage/v1/object/upload/sign/subidas/x.jpg?token=t',
    );
    expect(urlPublica(`${PUBLICO}imagenes/x.jpg`)).toBe(`${PUBLICO}imagenes/x.jpg`);
  });
});
