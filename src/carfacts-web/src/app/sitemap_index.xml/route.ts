import { SITE_CONFIG } from "@/lib/site-config";

export const dynamic = "force-static";

export async function GET() {
  const base = SITE_CONFIG.baseUrl;

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <sitemap>
    <loc>${base}/sitemap.xml</loc>
  </sitemap>
</sitemapindex>
`;

  return new Response(xml, {
    headers: { "Content-Type": "application/xml; charset=utf-8" },
  });
}
