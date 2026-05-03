import type { MetadataRoute } from "next";
import { getAllPosts } from "@/lib/posts";
import { SITE_CONFIG } from "@/lib/site-config";

export const dynamic = "force-static";

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const posts = await getAllPosts();
  const base = SITE_CONFIG.baseUrl;

  const staticRoutes: MetadataRoute.Sitemap = [
    { url: `${base}/`, priority: 1.0, changeFrequency: "daily" },
    { url: `${base}/archive/`, priority: 0.8, changeFrequency: "daily" },
    { url: `${base}/about/`, priority: 0.5, changeFrequency: "monthly" },
  ];

  const postRoutes: MetadataRoute.Sitemap = posts.map((post) => {
    const [year, month, day] = post.publishedAt.split("T")[0].split("-");
    return {
      url: `${base}/${year}/${month}/${day}/${post.slug}/`,
      lastModified: new Date(post.publishedAt),
      priority: 0.7,
      changeFrequency: "never",
    };
  });

  return [...staticRoutes, ...postRoutes];
}
