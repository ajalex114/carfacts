import { getAllPosts } from "@/lib/posts";
import { SITE_CONFIG } from "@/lib/site-config";

export const dynamic = "force-static";

function escapeXml(str = "") {
  return str
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

export async function GET() {
  const posts = await getAllPosts();
  const base = SITE_CONFIG.baseUrl;

  const items = posts.slice(0, 50).map((post) => {
    const [year, month, day] = post.publishedAt.split("T")[0].split("-");
    const link = `${base}/${year}/${month}/${day}/${post.slug}/`;
    return `
  <item>
    <title>${escapeXml(post.title)}</title>
    <link>${link}</link>
    <guid isPermaLink="true">${link}</guid>
    <pubDate>${new Date(post.publishedAt).toUTCString()}</pubDate>
    <description>${escapeXml(post.subtitle)}</description>
  </item>`;
  }).join("");

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom">
  <channel>
    <title>${escapeXml(SITE_CONFIG.name)}</title>
    <link>${base}</link>
    <description>Five car facts. Every day. Forever.</description>
    <language>en-us</language>
    <atom:link href="${base}/feed/" rel="self" type="application/rss+xml"/>
    <lastBuildDate>${new Date().toUTCString()}</lastBuildDate>${items}
  </channel>
</rss>
`;

  return new Response(xml, {
    headers: { "Content-Type": "application/rss+xml; charset=utf-8" },
  });
}
