/**
 * RSS feed endpoint — proxies the feed.xml written to Azure Blob Storage
 * by the Azure Function (RssFeedGeneratorService).
 *
 * In development, returns a minimal stub feed.
 * In production, the SWA config rewrites /feed/ → /api/feed.
 */

const BLOB_FEEDS_URL = process.env.BLOB_FEEDS_URL ?? "";

export async function GET() {
  // Production: serve from Blob Storage
  if (BLOB_FEEDS_URL) {
    try {
      const res = await fetch(`${BLOB_FEEDS_URL}/feed.xml`, {
        next: { revalidate: 3600 },
      });
      if (res.ok) {
        const xml = await res.text();
        return new Response(xml, {
          headers: {
            "Content-Type": "application/rss+xml; charset=utf-8",
            "Cache-Control": "public, max-age=3600, s-maxage=3600",
          },
        });
      }
    } catch {
      // Fall through to stub
    }
  }

  // Development stub
  const stub = `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom">
  <channel>
    <title>Car Facts Daily</title>
    <link>https://carfactsdaily.com</link>
    <description>Daily car facts — five per day, every day.</description>
    <atom:link href="https://carfactsdaily.com/feed/" rel="self" type="application/rss+xml"/>
  </channel>
</rss>`;

  return new Response(stub, {
    headers: { "Content-Type": "application/rss+xml; charset=utf-8" },
  });
}
