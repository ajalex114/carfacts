import { getAllPosts } from "@/lib/posts";
import { SITE_CONFIG } from "@/lib/site-config";

export const dynamic = "force-static";

function escapeXml(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

export async function GET() {
  const posts = await getAllPosts();
  const base = SITE_CONFIG.baseUrl;

  const urls = posts
    .filter((post) => post.heroImageUrl)
    .map((post) => {
      const postUrl = post.postUrl.startsWith("http")
        ? post.postUrl
        : `${base}${post.postUrl}`;
      return `  <url>
    <loc>${escapeXml(postUrl)}</loc>
    <image:image>
      <image:loc>${escapeXml(post.heroImageUrl)}</image:loc>
      <image:title>${escapeXml(post.title)}</image:title>
    </image:image>
  </url>`;
    });

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:image="http://www.google.com/schemas/sitemap-image/1.1">
${urls.join("\n")}
</urlset>`;

  return new Response(xml, {
    headers: { "Content-Type": "application/xml" },
  });
}
