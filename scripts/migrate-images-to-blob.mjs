#!/usr/bin/env node
/**
 * Migrates WordPress/Unsplash post images into Azure Blob Storage.
 *
 * Strategy (in order):
 *   1. If featuredImageUrl is a valid non-placeholder URL → download & upload
 *   2. Extract first <img src> from htmlContent (WordPress post body)
 *   3. Fall back to a curated set of car Unsplash photos (rotated by post index)
 *
 * Usage:
 *   node scripts/migrate-images-to-blob.mjs
 *
 * Required env vars:
 *   COSMOS_ENDPOINT        e.g. https://cosmos-carfacts5.documents.azure.com:443/
 *   COSMOS_KEY             Cosmos DB primary key
 *   BLOB_ACCOUNT_NAME      e.g. stblobcarfacts5
 *   BLOB_ACCOUNT_KEY       Storage account key
 *
 * Optional:
 *   BLOB_CONTAINER         defaults to "post-images"
 *   DRY_RUN=1              log only, no writes
 */

import { CosmosClient } from "@azure/cosmos";
import { BlobServiceClient, StorageSharedKeyCredential } from "@azure/storage-blob";

// ── Config ──────────────────────────────────────────────────────────────────
const COSMOS_ENDPOINT = process.env.COSMOS_ENDPOINT;
const COSMOS_KEY      = process.env.COSMOS_KEY;
const BLOB_ACCOUNT    = process.env.BLOB_ACCOUNT_NAME;
const BLOB_KEY        = process.env.BLOB_ACCOUNT_KEY;
const BLOB_CONTAINER  = process.env.BLOB_CONTAINER ?? "post-images";
const DRY_RUN         = process.env.DRY_RUN === "1";

for (const [name, val] of [
  ["COSMOS_ENDPOINT", COSMOS_ENDPOINT],
  ["COSMOS_KEY",      COSMOS_KEY],
  ["BLOB_ACCOUNT_NAME", BLOB_ACCOUNT],
  ["BLOB_ACCOUNT_KEY",  BLOB_KEY],
]) {
  if (!val) { console.error(`❌  Missing env var: ${name}`); process.exit(1); }
}

if (DRY_RUN) console.log("⚠️  DRY_RUN mode — no writes will happen\n");

// ── Curated fallback car images (Unsplash, varied subjects) ──────────────────
const FALLBACK_IMAGES = [
  "https://images.unsplash.com/photo-1492144534655-ae79c964c9d7?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1503376780353-7e6692767b70?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1568605117036-5fe5e7bab0b3?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1525609004556-c46c7d6cf023?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1619642751034-765dfdf7c58e?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1541773367336-d3f9c7f8bc4e?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1606664515524-ed2f786a0bd6?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1601584115197-04ecc0da31d7?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1487754180451-c456f719a1fc?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1550355291-bbee04a92027?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1544636331-e26879cd4d9b?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1590362891991-f776e747a588?w=1600&q=80&auto=format&fit=crop",
  "https://images.unsplash.com/photo-1533473359331-0135ef1b58bf?w=1600&q=80&auto=format&fit=crop",
];

// ── Clients ─────────────────────────────────────────────────────────────────
const cosmos    = new CosmosClient({ endpoint: COSMOS_ENDPOINT, key: COSMOS_KEY });
const postsContainer = cosmos.database("carfacts").container("posts");

const credential    = new StorageSharedKeyCredential(BLOB_ACCOUNT, BLOB_KEY);
const blobService   = new BlobServiceClient(
  `https://${BLOB_ACCOUNT}.blob.core.windows.net`,
  credential
);
const blobContainer = blobService.getContainerClient(BLOB_CONTAINER);

// ── Helpers ──────────────────────────────────────────────────────────────────
const BROKEN_PATTERNS = [
  /car-facts\.png$/,
  /\/uploads\/car-facts/,
];
function isBrokenImage(url) {
  if (!url) return true;
  return BROKEN_PATTERNS.some(p => p.test(url));
}

/** Extract first <img src="..."> from HTML */
function extractFirstImageUrl(html) {
  if (!html) return null;
  const match = html.match(/<img[^>]+src=["']([^"']+)["']/i);
  return match ? match[1] : null;
}

async function downloadImage(url) {
  const resp = await fetch(url, {
    headers: { "User-Agent": "CarFactsDaily-ImageMigration/1.0" },
    redirect: "follow",
  });
  if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
  const buffer = await resp.arrayBuffer();
  const contentType = resp.headers.get("content-type") ?? "image/jpeg";
  return { data: Buffer.from(buffer), contentType };
}

async function uploadToBlob(docId, imageBuffer, contentType) {
  // Always store as .jpg in blob for simplicity
  const blobPath   = `posts/${docId}/hero.jpg`;
  const blobClient = blobContainer.getBlockBlobClient(blobPath);
  if (!DRY_RUN) {
    await blobClient.uploadData(imageBuffer, {
      blobHTTPHeaders: {
        blobContentType: contentType.includes("png") ? "image/png" : "image/jpeg",
        blobCacheControl: "public, max-age=31536000, immutable",
      },
    });
  }
  return { blobPath, blobUrl: blobClient.url };
}

// ── Fetch all Cosmos documents ───────────────────────────────────────────────
console.log("📦  Fetching posts from Cosmos DB...");
const { resources: docs } = await postsContainer.items
  .query("SELECT c.id, c.partitionKey, c.featuredImageUrl, c.images, c.htmlContent FROM c")
  .fetchAll();

console.log(`    Found ${docs.length} posts\n`);

// ── Migrate ──────────────────────────────────────────────────────────────────
let success = 0, skipped = 0, failed = 0;

for (let i = 0; i < docs.length; i++) {
  const doc = docs[i];
  const { id: docId, partitionKey, featuredImageUrl, htmlContent } = doc;

  // Skip if already in our blob storage
  if (featuredImageUrl?.includes(BLOB_ACCOUNT)) {
    console.log(`  [${i + 1}/${docs.length}] ⏭  ${docId} (already in blob)`);
    skipped++;
    continue;
  }

  // Determine best image URL to fetch
  let sourceUrl = null;
  let sourceType = "";

  if (!isBrokenImage(featuredImageUrl)) {
    sourceUrl = featuredImageUrl;
    sourceType = "featured";
  } else {
    const inlineImg = extractFirstImageUrl(htmlContent);
    if (inlineImg && !isBrokenImage(inlineImg)) {
      sourceUrl = inlineImg;
      sourceType = "inline";
    } else {
      // Use curated fallback, rotated by index for variety
      sourceUrl = FALLBACK_IMAGES[i % FALLBACK_IMAGES.length];
      sourceType = "fallback";
    }
  }

  try {
    process.stdout.write(`  [${i + 1}/${docs.length}] [${sourceType}] ⬇  ${docId.substring(0, 50)} ...`);

    const { data, contentType } = await downloadImage(sourceUrl);
    const { blobPath, blobUrl } = await uploadToBlob(docId, data, contentType);

    if (!DRY_RUN) {
      const itemRef = postsContainer.item(docId, partitionKey);
      const { resource: fullDoc } = await itemRef.read();
      fullDoc.featuredImageUrl = blobUrl;
      if (Array.isArray(fullDoc.images) && fullDoc.images.length > 0) {
        fullDoc.images[0].blobUrl  = blobUrl;
        fullDoc.images[0].blobPath = blobPath;
      }
      await itemRef.replace(fullDoc);
    }

    console.log(` ✓`);
    success++;

    await new Promise(r => setTimeout(r, 300));
  } catch (err) {
    console.log(` ✗  ${err.message}`);
    failed++;
  }
}

// ── Summary ──────────────────────────────────────────────────────────────────
console.log(`
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Migrated: ${success}
  Skipped:  ${skipped} (already in blob)
  Failed:   ${failed}
  Blob URL: https://${BLOB_ACCOUNT}.blob.core.windows.net/${BLOB_CONTAINER}/posts/
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`);

if (failed > 0) process.exit(1);
