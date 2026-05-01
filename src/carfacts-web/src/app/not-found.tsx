import Link from "next/link";
import SiteHeader from "@/components/layout/SiteHeader";
import SiteFooter from "@/components/layout/SiteFooter";

export default function NotFound() {
  return (
    <>
      <SiteHeader />
      <main className="flex-1 flex items-center justify-center">
        <div className="mx-auto max-w-xl px-6 py-24 text-center">
          <p className="kicker text-signal mb-4">404</p>
          <h1 className="font-display text-5xl font-bold tracking-tight leading-[0.95] mb-6 md:text-7xl">
            Page not found.
          </h1>
          <p className="text-muted-foreground text-lg mb-10">
            This issue doesn&apos;t exist — yet. Five new facts arrive every day.
          </p>
          <Link
            href="/"
            className="inline-flex items-center gap-2 border border-foreground bg-foreground px-6 py-3 text-sm font-semibold text-background transition-colors hover:bg-signal hover:border-signal"
          >
            ← Back to today&apos;s issue
          </Link>
        </div>
      </main>
      <SiteFooter />
    </>
  );
}
