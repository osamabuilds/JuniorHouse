/** Warehouse app APP-7 and APP-10: the admin app can be installed to the home screen and is still never indexed. */
interface FileSystem {
  readFileSync(path: string, encoding: string): string;
  existsSync(path: string): boolean;
}

export {};

// The spec reads the real files the build serves. The module name is a variable so the browser-typed compile does not look for Node types.
const nodeFs = 'node:fs';
const fs = (await import(/* @vite-ignore */ nodeFs)) as FileSystem;

describe('installable admin app (spec APP-7, APP-10)', () => {
  const root = 'projects/admin';
  const manifest = JSON.parse(fs.readFileSync(`${root}/public/manifest.webmanifest`, 'utf8')) as {
    name: string;
    display: string;
    start_url: string;
    icons: { src: string; sizes: string; type: string }[];
  };
  const indexHtml = fs.readFileSync(`${root}/src/index.html`, 'utf8');

  it('APP-7 has a web manifest that opens full screen on the pick queue', () => {
    expect(manifest.name).toBeTruthy();
    expect(manifest.display).toBe('standalone');
    expect(manifest.start_url).toBe('/fulfilment/pick');
    expect(indexHtml).toContain('rel="manifest"');
  });

  it('APP-7 names a 192 and a 512 pixel icon, and the files exist', () => {
    const sizes = manifest.icons.map((i) => i.sizes);

    expect(sizes).toContain('192x192');
    expect(sizes).toContain('512x512');
    for (const icon of manifest.icons) {
      expect(icon.type).toBe('image/png');
      expect(fs.existsSync(`${root}/public/${icon.src}`)).toBe(true);
    }
  });

  it('APP-10 the admin app still sends noindex and nofollow', () => {
    expect(indexHtml).toContain('<meta name="robots" content="noindex, nofollow" />');
  });

  it('NFR-17 the page has a mobile viewport', () => {
    expect(indexHtml).toContain('name="viewport" content="width=device-width, initial-scale=1"');
  });
});
