/**
 * One-off script: strips leading "TIL " / "TIL: " (case-insensitive) from
 * excerpt, metaDescription, and facts[].fact fields in the Cosmos DB posts container.
 *
 * Usage (from carfacts-web dir, after `az login`):
 *   node scripts/strip-til.mjs
 */

import { CosmosClient } from "@azure/cosmos";
import { DefaultAzureCredential } from "@azure/identity";

const ENDPOINT = process.env.COSMOS_ENDPOINT ?? "https://cosmos-carfacts5.documents.azure.com:443/";
const DB = "carfacts";
const CONTAINER = "posts";

const TIL_RE = /^TIL[:\s]+/i;

function stripTil(str) {
  return str ? str.replace(TIL_RE, "") : str;
}

const credential = new DefaultAzureCredential();
const client = new CosmosClient({ endpoint: ENDPOINT, aadCredentials: credential });
const container = client.database(DB).container(CONTAINER);

const { resources: docs } = await container.items.query("SELECT * FROM c").fetchAll();

let updated = 0;
let skipped = 0;

for (const doc of docs) {
  let dirty = false;

  if (doc.excerpt && TIL_RE.test(doc.excerpt)) {
    doc.excerpt = stripTil(doc.excerpt);
    dirty = true;
  }
  if (doc.metaDescription && TIL_RE.test(doc.metaDescription)) {
    doc.metaDescription = stripTil(doc.metaDescription);
    dirty = true;
  }
  if (Array.isArray(doc.facts)) {
    for (const f of doc.facts) {
      if (f.fact && TIL_RE.test(f.fact)) {
        f.fact = stripTil(f.fact);
        dirty = true;
      }
    }
  }

  if (dirty) {
    await container.items.upsert(doc);
    console.log(`✅ Updated: ${doc.slug}`);
    updated++;
  } else {
    skipped++;
  }
}

console.log(`\nDone. ${updated} documents updated, ${skipped} already clean.`);
