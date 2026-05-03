import type { Metadata } from "next";
import SiteHeader from "@/components/layout/SiteHeader";
import SiteFooter from "@/components/layout/SiteFooter";
import { SITE_CONFIG } from "@/lib/site-config";

export const metadata: Metadata = {
  title: "Stories",
  description: `Web Stories from ${SITE_CONFIG.name} — bite-sized automotive facts in a visual format.`,
};

export default function StoriesPage() {
  return (
    <>
      <SiteHeader />

      <main className="flex-1">
        <div className="mx-auto max-w-7xl px-6 py-10 md:py-16">
          <header className="border-b border-border pb-10">
            <p className="kicker text-signal mb-3">Stories</p>
            <h1 className="font-display text-5xl font-bold tracking-tight leading-[0.95] md:text-7xl">
              Web Stories
            </h1>
            <p className="mt-4 text-lg text-muted-foreground max-w-xl">
              Bite-sized automotive facts in a visual, swipeable format.
            </p>
          </header>

          <div className="mt-16 text-center text-muted-foreground py-24">
            <p className="font-display text-2xl font-medium text-foreground mb-3">Coming soon</p>
            <p className="text-base">Stories are on their way. Check back daily.</p>
          </div>
        </div>
      </main>

      <SiteFooter />
    </>
  );
}
