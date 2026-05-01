#!/usr/bin/env node
// Migrates all published WordPress posts to Cosmos DB.
// Run: node scripts/migrate-to-cosmos.mjs
// Requires env vars: COSMOS_ENDPOINT, COSMOS_KEY

import { CosmosClient } from "@azure/cosmos";

const WP_API = "https://public-api.wordpress.com/wp/v2/sites/carfactsdaily.com";
const SITE_BASE = "https://carfactsdaily.com";
const COSMOS_ENDPOINT = process.env.COSMOS_ENDPOINT;
const COSMOS_KEY = process.env.COSMOS_KEY;

if (!COSMOS_ENDPOINT || !COSMOS_KEY) {
  console.error("❌ COSMOS_ENDPOINT and COSMOS_KEY env vars are required.");
  process.exit(1);
}

// ── Fetch all published WP posts ────────────────────────────────────────────
async function fetchAllPosts() {
  const allPosts = [];
  let page = 1;
  while (true) {
    const url = `${WP_API}/posts?status=publish&per_page=100&page=${page}&_embed=1&_fields=id,slug,title,date,content,excerpt,featured_media,_embedded`;
    const resp = await fetch(url);
    if (!resp.ok) break;
    const posts = await resp.json();
    if (!posts || posts.length === 0) break;
    allPosts.push(...posts);
    if (posts.length < 100) break;
    page++;
  }
  return allPosts;
}

// ── Main ────────────────────────────────────────────────────────────────────
const client = new CosmosClient({ endpoint: COSMOS_ENDPOINT, key: COSMOS_KEY });
const container = client.database("carfacts").container("posts");

console.log("Fetching WordPress posts...");
const wpPosts = await fetchAllPosts();
console.log(`  Found ${wpPosts.length} posts`);

// Sort ascending (oldest first) so issueNumber=1 is the earliest post
wpPosts.sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());

let success = 0, failed = 0;

for (let i = 0; i < wpPosts.length; i++) {
  const post = wpPosts[i];
  const date = new Date(post.date);
  const year  = date.getFullYear().toString();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day   = String(date.getDate()).padStart(2, "0");
  const slug  = post.slug;
  const docId   = `${year}-${month}-${day}_${slug}`;
  const partKey = `${year}-${month}`;

  // Featured image — prefer i0.wp.com CDN for resilience
  let imgUrl = "";
  let imgAlt = "";
  try {
    const fm = post._embedded?.["wp:featuredmedia"]?.[0];
    if (fm?.source_url) {
      imgUrl = fm.source_url.replace(/^https?:\/\/carfactsdaily\.com/, "https://i0.wp.com/carfactsdaily.com");
      imgAlt = fm.alt_text || post.title.rendered.replace(/<[^>]+>/g, "");
    }
  } catch {}
  if (!imgUrl) imgUrl = "https://i0.wp.com/carfactsdaily.com/wp-content/uploads/car-facts.png";

  const titleText   = post.title.rendered.replace(/<[^>]+>/g, "").trim();
  const excerptText = post.excerpt.rendered.replace(/<[^>]+>/g, "").replace(/\n+/g, " ").trim();
  const publishedAt = date.toISOString();
  const metaDesc    = excerptText.substring(0, 160) || titleText;

  const doc = {
    id: docId,
    partitionKey: partKey,
    slug,
    postUrl: `${SITE_BASE}/${year}/${month}/${day}/${slug}/`,
    wordPressPostId: post.id,
    title: titleText,
    metaDescription: metaDesc,
    excerpt: excerptText,
    htmlContent: post.content.rendered,
    featuredImageUrl: imgUrl,
    images: [{ factIndex: 0, blobUrl: imgUrl, blobPath: "", altText: imgAlt, title: titleText, caption: "" }],
    keywords: [],
    tags: [],
    socialHashtags: [],
    category: "car-facts",
    author: "thecargeek",
    publishedAt,
    facts: [],
    createdAt: new Date().toISOString(),
    migrated: true,
  };

  try {
    await container.items.upsert(doc);
    console.log(`  [${i + 1}/${wpPosts.length}] ✓  ${docId}`);
    success++;
  } catch (err) {
    console.error(`  [${i + 1}/${wpPosts.length}] ✗  ${docId}:`, err.message);
    failed++;
  }
}

console.log(`\nDone: ${success} success, ${failed} failed`);
