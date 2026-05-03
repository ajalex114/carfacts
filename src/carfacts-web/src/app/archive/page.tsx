import type { Metadata } from "next";
import SiteHeader from "@/components/layout/SiteHeader";
import SiteFooter from "@/components/layout/SiteFooter";
import ArchiveSearch from "@/components/posts/ArchiveSearch";
import { getAllPosts } from "@/lib/posts";
import { SITE_CONFIG } from "@/lib/site-config";

export const metadata: Metadata = {
  title: "Archive",
  description: `Every issue of ${SITE_CONFIG.name}. Browse all car facts by date.`,
};

export default async function ArchivePage() {
  const posts = await getAllPosts();

  return (
    <>
      <SiteHeader />

      <main className="flex-1">
        <div className="mx-auto max-w-7xl px-6 py-10 md:py-16">
          {/* Page header */}
          <header className="border-b border-border pb-10 md:pb-14">
            <p className="kicker text-signal mb-3">Archive</p>
            <h1 className="font-display text-5xl font-bold tracking-tight leading-[0.95] md:text-7xl">
              Every issue.
              <br />
              Every fact.
            </h1>
            <p className="mt-4 text-muted-foreground md:text-lg">
              {posts.length} issues.&nbsp;
              {posts.length * 5} historic car facts.
            </p>
          </header>

          {/* Search + filtered list */}
          <ArchiveSearch posts={posts} />
        </div>
      </main>

      <SiteFooter />
    </>
  );
}
