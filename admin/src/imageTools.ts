import { api, type MediaItem } from './api';

export const MAX_MAIN = 2000;
export const VARIANTS = [320, 480, 640, 800];

export interface Prepared {
  main: Blob;
  width: number;
  height: number;
  variants: Record<number, Blob>;
  fileName: string;
}

function encode(canvas: HTMLCanvasElement, type: string, quality: number): Promise<Blob | null> {
  return new Promise(resolve => canvas.toBlob(resolve, type, quality));
}

async function render(bitmap: ImageBitmap, targetWidth: number): Promise<{ blob: Blob; ext: string; w: number; h: number }> {
  const w = Math.min(targetWidth, bitmap.width);
  const h = Math.round(bitmap.height * (w / bitmap.width));
  const canvas = document.createElement('canvas');
  canvas.width = w;
  canvas.height = h;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Your browser cannot process images.');
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(bitmap, 0, 0, w, h);

  let blob = await encode(canvas, 'image/webp', 0.82);
  let ext = 'webp';
  if (!blob || blob.type !== 'image/webp') { blob = await encode(canvas, 'image/jpeg', 0.85); ext = 'jpg'; } // old Safari has no WebP encoder
  if (!blob) throw new Error('Could not process that image.');
  return { blob, ext, w, h };
}

/**
 * Makes a web-ready main image (max 2000px wide) plus smaller copies for responsive srcset.
 * Animated GIFs are uploaded untouched.
 */
export async function prepareImage(file: File): Promise<Prepared> {
  if (file.type === 'image/gif') {
    const bmp = await createImageBitmap(file);
    const out = { main: file, width: bmp.width, height: bmp.height, variants: {}, fileName: file.name };
    bmp.close();
    return out;
  }

  let bitmap: ImageBitmap;
  try { bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' }); }
  catch { throw new Error('That file could not be read as an image. Please use a JPG, PNG or WebP file.'); }

  try {
    const main = await render(bitmap, MAX_MAIN);
    const variants: Record<number, Blob> = {};
    for (const w of VARIANTS) {
      if (w >= bitmap.width) continue;
      const v = await render(bitmap, w);
      if (v.ext === main.ext) variants[w] = v.blob;
    }
    const base = file.name.replace(/\.[^.]+$/, '') || 'image';
    return { main: main.blob, width: main.w, height: main.h, variants, fileName: `${base}.${main.ext}` };
  } finally { bitmap.close(); }
}

export async function uploadImage(file: File, alt = ''): Promise<MediaItem> {
  const p = await prepareImage(file);
  const form = new FormData();
  form.append('file', p.main, p.fileName);
  for (const [w, blob] of Object.entries(p.variants)) form.append(`v${w}`, blob, `${w}.img`);
  form.append('width', String(p.width));
  form.append('height', String(p.height));
  if (alt) form.append('alt', alt);
  return api.upload<MediaItem>('/api/admin/media', form);
}
