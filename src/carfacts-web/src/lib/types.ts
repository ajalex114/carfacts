/**
 * Core post types used across all UI components.
 * These are the "view model" types — Cosmos DB documents are mapped to these
 * before being passed to components.
 */

export interface PostFact {
  title: string;
  body: string;
  imageUrl: string;
  imageAlt: string;
}

export interface Post {
  id: string;
  /** Sequential issue number (1 = first post ever, n = today's post). */
  issueNumber: number;
  slug: string;
  /** Canonical URL: /YYYY/MM/DD/slug/ */
  postUrl: string;
  title: string;
  /** One-liner shown under the title and in archive/home cards. Sourced from excerpt. */
  subtitle: string;
  heroImageUrl: string;
  heroImageAlt: string;
  /** 1–2 sentence intro paragraph shown above the five facts. */
  intro: string;
  facts: PostFact[];
  /** ISO-8601 datetime string */
  publishedAt: string;
  category: string;
  keywords: string[];
  metaDescription: string;
}
