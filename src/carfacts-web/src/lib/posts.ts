import { CosmosClient } from "@azure/cosmos";
import type { Post, PostFact } from "./types";
import { MOCK_POSTS } from "./mock-data";

// Decode numeric and common named HTML entities that WordPress encodes in API responses
function decodeHtmlEntities(str: string): string {
  if (!str) return str;
  return str
    .replace(/&#(\d+);/g, (_, code) => String.fromCodePoint(Number(code)))
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&#039;/g, "'")
    .replace(/&nbsp;/g, "\u00a0");
}

// ---------------------------------------------------------------------------
// Cosmos DB document shapes (mirror the C# models in CarFacts.Functions)
// ---------------------------------------------------------------------------

interface CosmosCarFact {
  year: number;
  catchy_title: string;
  fact: string;
  car_model: string;
  image_prompt: string;
  image_search_query: string;
}

interface CosmosPostImage {
  factIndex: number;
  blobUrl: string;
  blobPath: string;
  altText: string;
  title: string;
  caption: string;
}

interface CosmosPostDocument {
  id: string;
  slug: string;
  postUrl: string;
  title: string;
  metaDescription: string;
  excerpt: string;
  htmlContent?: string;
  featuredImageUrl: string;
  images: CosmosPostImage[];
  keywords: string[];
  category: string;
  publishedAt: string;
  facts: CosmosCarFact[];
}

// ---------------------------------------------------------------------------
// Mapper: CosmosPostDocument → Post (view model)
// ---------------------------------------------------------------------------

// Fallback image for posts that have no featured media
const FALLBACK_IMAGE = "https://images.unsplash.com/photo-1492144534655-ae79c964c9d7?w=800&q=80&auto=format&fit=crop";
const BROKEN_IMAGE_PATTERN = /car-facts\.png$/;

function cosmosDocToPost(doc: CosmosPostDocument, issueNumber: number): Post {
  const heroImageUrl =
    doc.featuredImageUrl && !BROKEN_IMAGE_PATTERN.test(doc.featuredImageUrl)
      ? doc.featuredImageUrl
      : FALLBACK_IMAGE;

  const facts: PostFact[] = (doc.facts ?? []).map((f, i) => {
    const img = doc.images?.find((im) => im.factIndex === i);
    return {
      title: f.catchy_title,
      body: f.fact,
      imageUrl: img?.blobUrl || heroImageUrl,
      imageAlt: img?.altText || f.catchy_title,
    };
  });

  return {
    id: doc.id,
    issueNumber,
    slug: doc.slug,
    postUrl: doc.postUrl,
    title: decodeHtmlEntities(doc.title),
    subtitle: decodeHtmlEntities(doc.excerpt || doc.metaDescription || ""),
    heroImageUrl,
    heroImageAlt: doc.images?.[0]?.altText || decodeHtmlEntities(doc.title),
    intro: decodeHtmlEntities(doc.excerpt || ""),
    facts,
    htmlContent: doc.htmlContent,
    publishedAt: doc.publishedAt,
    category: doc.category || "car-facts",
    keywords: doc.keywords || [],
    metaDescription: decodeHtmlEntities(doc.metaDescription || doc.excerpt || ""),
  };
}

// ---------------------------------------------------------------------------
// Data fetching — Cosmos DB (build-time only; falls back to mock data)
// ---------------------------------------------------------------------------

async function fetchFromCosmos(): Promise<Post[]> {
  const endpoint = process.env.COSMOS_ENDPOINT;
  const key = process.env.COSMOS_KEY;

  if (!endpoint || !key) return [];

  try {
    const client = new CosmosClient({ endpoint, key });
    const container = client.database("carfacts").container("posts");

    // Fetch oldest-first so we can assign issue numbers sequentially (1 = first post)
    const query = "SELECT * FROM c ORDER BY c.publishedAt ASC";
    const { resources } = await container.items
      .query<CosmosPostDocument>(query)
      .fetchAll();

    // Map to Post view models; reverse so newest is first for display
    return resources
      .map((doc, index) => cosmosDocToPost(doc, index + 1))
      .reverse();
  } catch (err) {
    console.warn("[posts] Cosmos DB query failed, falling back to mock data:", err);
    return [];
  }
}

// ---------------------------------------------------------------------------
// In-memory cache — getAllPosts() is called many times during static build
// ---------------------------------------------------------------------------

let _cache: Post[] | null = null;

export async function getAllPosts(): Promise<Post[]> {
  if (_cache) return _cache;

  const cosmosPosts = await fetchFromCosmos();
  if (cosmosPosts.length > 0) {
    _cache = cosmosPosts;
    return _cache;
  }

  // Fallback: convert mock data to the Post view model shape
  _cache = MOCK_POSTS.map((m) => ({
    id: m.id,
    issueNumber: m.issueNumber,
    slug: m.slug,
    postUrl: m.postUrl,
    title: m.title,
    subtitle: m.subtitle,
    heroImageUrl: m.heroImageUrl,
    heroImageAlt: m.heroImageAlt,
    intro: m.intro,
    facts: m.facts.map((f) => ({
      title: f.title,
      body: f.body,
      imageUrl: f.imageUrl,
      imageAlt: f.imageAlt,
    })),
    publishedAt: m.publishedAt,
    category: m.category,
    keywords: m.keywords,
    metaDescription: m.subtitle,
  }));
  return _cache;
}

export async function getLatestPost(): Promise<Post | null> {
  const posts = await getAllPosts();
  return posts[0] ?? null;
}

/** Returns all posts except the most recent one (for the "past issues" grid). */
export async function getPastPosts(): Promise<Post[]> {
  const posts = await getAllPosts();
  return posts.slice(1);
}

export async function getPostByParams(
  year: string,
  month: string,
  day: string,
  slug: string
): Promise<Post | null> {
  const posts = await getAllPosts();
  return (
    posts.find(
      (p) =>
        p.slug === slug &&
        p.postUrl.includes(`/${year}/${month}/${day}/`)
    ) ?? null
  );
}

/** All route params for generateStaticParams(). */
export async function getAllPostParams(): Promise<
  { year: string; month: string; day: string; slug: string }[]
> {
  const posts = await getAllPosts();
  return posts.map((post) => {
    const [year, month, day] = post.publishedAt.split("T")[0].split("-");
    return { year, month, day, slug: post.slug };
  });
}
