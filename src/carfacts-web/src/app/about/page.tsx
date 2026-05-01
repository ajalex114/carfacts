import type { Metadata } from "next";
import SiteHeader from "@/components/layout/SiteHeader";
import SiteFooter from "@/components/layout/SiteFooter";
import { SITE_CONFIG } from "@/lib/site-config";

export const metadata: Metadata = {
  title: "About",
  description: `About ${SITE_CONFIG.name} — five automotive facts, every day.`,
};

export default function AboutPage() {
  return (
    <>
      <SiteHeader />

      <main className="flex-1">
        <div className="mx-auto max-w-3xl px-6 py-10 md:py-16">
          <header className="border-b border-border pb-10">
            <p className="kicker text-signal mb-3">About</p>
            <h1 className="font-display text-5xl font-bold tracking-tight leading-[0.95] md:text-7xl">
              Five facts.
              <br />
              Every day.
            </h1>
          </header>

          <div className="mt-10 space-y-6 text-base leading-relaxed text-muted-foreground md:text-lg">
            <p>
              <span className="font-semibold text-foreground">{SITE_CONFIG.name}</span> publishes
              five carefully researched facts about cars, engines, motorsport, and automotive
              history — every single day.
            </p>
            <p>
              Each fact is grounded in primary sources: engineering textbooks, period newspaper
              accounts, patent filings, and manufacturer records. We don&apos;t recycle the same
              trivia that&apos;s been circulating the internet for a decade.
            </p>
            <p>
              The goal is simple: you close the tab knowing something true and interesting that
              you didn&apos;t know when you opened it.
            </p>

            <blockquote className="border-l-2 border-signal pl-6 font-display text-2xl font-medium text-foreground leading-snug">
              {SITE_CONFIG.editorNote.quote}
            </blockquote>

            <p className="text-sm text-muted-foreground">
              {SITE_CONFIG.editorNote.attribution}
            </p>
          </div>
        </div>
      </main>

      <SiteFooter />
    </>
  );
}
