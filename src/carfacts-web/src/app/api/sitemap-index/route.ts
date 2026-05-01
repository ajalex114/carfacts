/**
 * Sitemap index endpoint — proxies sitemap_index.xml from Azure Blob Storage.
 * SWA route: /sitemap_index.xml → /api/sitemap-index
 */

const BLOB_FEEDS_URL = process.env.BLOB_FEEDS_URL ?? "";

export async function GET() {
  if (BLOB_FEEDS_URL) {
    try {
      const res = await fetch(`${BLOB_FEEDS_URL}/sitemap_index.xml`, {
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
<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <sitemap><loc>https://carfactsdaily.com/post-sitemap.xml</loc></sitemap>
  <sitemap><loc>https://carfactsdaily.com/news-sitemap.xml</loc></sitemap>
</sitemapindex>`;

  return new Response(stub, {
    headers: { "Content-Type": "application/xml; charset=utf-8" },
  });
}
