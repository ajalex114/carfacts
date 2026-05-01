/**
 * Post sitemap endpoint — proxies post-sitemap.xml from Azure Blob Storage.
 * SWA route: /post-sitemap.xml → /api/sitemap-posts
 */

const BLOB_FEEDS_URL = process.env.BLOB_FEEDS_URL ?? "";

export async function GET() {
  if (BLOB_FEEDS_URL) {
    try {
      const res = await fetch(`${BLOB_FEEDS_URL}/post-sitemap.xml`, {
        next: { revalidate: 3600 },
      });
      if (res.ok) {
        const xml = await res.text();
        return new Response(xml, {
          headers: {
            "Content-Type": "application/xml; charset=utf-8",
            "Cache-Control": "public, max-age=3600, s-maxage=3600",
          },
        });
      }
    } catch {
      // Fall through to stub
    }
  }

  const stub = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"
        xmlns:image="http://www.google.com/schemas/sitemap-image/1.1">
</urlset>`;

  return new Response(stub, {
    headers: { "Content-Type": "application/xml; charset=utf-8" },
  });
}
