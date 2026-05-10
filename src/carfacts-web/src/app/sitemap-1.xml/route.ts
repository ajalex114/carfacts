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

  const urls = posts.map((post) => {
    const date = post.publishedAt.split("T")[0];
    return `  <url>
    <loc>${escapeXml(post.postUrl.startsWith("http") ? post.postUrl : `${base}${post.postUrl}`)}</loc>
    <lastmod>${date}</lastmod>
    <changefreq>monthly</changefreq>
    <priority>0.8</priority>
  </url>`;
  });

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls.join("\n")}
</urlset>`;

  return new Response(xml, {
    headers: { "Content-Type": "application/xml" },
  });
}
