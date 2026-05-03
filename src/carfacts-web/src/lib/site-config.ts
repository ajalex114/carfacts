/**
 * SITE CONFIGURATION — the single source of truth for branding & theme.
 *
 * To change the theme:
 *   1. Edit colour tokens in globals.css  (--color-signal, --background, etc.)
 *   2. Edit display font in layout.tsx    (swap the Google Font import)
 *   3. Edit text content here             (name, tagline, nav, editor note)
 *
 * Page templates read everything from this file — no per-page edits needed.
 */

export const SITE_CONFIG = {
  /** Shown in <title> tags and meta */
  name: "Car Facts Daily",

  /**
   * Logo rendered as  "Car[.]Facts[.]Daily"  where [.] is coloured signal-red.
   * Change wordA/wordB/wordC to rename without touching the header component.
   */
  logoWords: ["Car", "Facts", "Daily"] as const,

  tagline: "Know your car\u2019s history",

  description:
    "Daily car facts and automotive knowledge. Discover interesting facts about cars, engines, history, and more — published every single day.",

  baseUrl: "https://carfactsdaily.com",

  /** Used in home page hero kicker */
  publicationName: "Car Facts Daily",

  /** The marquee strip content — randomized car brands at runtime */
  marqueeItems: [
    "Mercedes-Benz", "BMW", "Tesla", "Ferrari", "Porsche",
  ],

  nav: [
    { label: "Today", href: "/" },
    { label: "Archive", href: "/archive" },
    { label: "About", href: "/about" },
  ],

  /** Displayed at the bottom of the home page */
  editorNote: {
    quote:
      "\u201cCars are the rolling autobiography of the twentieth century. Five facts a day, and we\u2019ll spend a lifetime barely scratching the paint.\u201d",
    attribution: "\u2014 The Editor",
  },

  footer: {
    copyright: `\u00a9 ${new Date().getFullYear()} Car Facts Daily. All rights reserved.`,
    links: [
      { label: "Privacy Policy", href: "/privacy" },
      { label: "RSS Feed", href: "/feed/" },
      { label: "Sitemap", href: "/sitemap_index.xml" },
    ],
  },

  /** Open Graph / Twitter card defaults */
  og: {
    twitterHandle: "@carfactsdaily",
    defaultImage: "https://carfactsdaily.com/og-image.png",
  },
} as const;

/** Format a JS Date as "Friday, May 1, 2026" */
export function formatHeaderDate(date: Date = new Date()): string {
  return date.toLocaleDateString("en-US", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

/** Format a JS Date as "May 1, 2026" */
export function formatDisplayDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString("en-US", {
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

/** Zero-pad issue number to 3 digits: 7 → "007" */
export function formatIssueNumber(n: number): string {
  return String(n).padStart(3, "0");
}
