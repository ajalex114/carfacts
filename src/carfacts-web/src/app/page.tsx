import Link from "next/link";
import SiteHeader from "@/components/layout/SiteHeader";
import SiteFooter from "@/components/layout/SiteFooter";
import Marquee from "@/components/layout/Marquee";
import HeroPost from "@/components/posts/HeroPost";
import PostCard from "@/components/posts/PostCard";
import { SITE_CONFIG } from "@/lib/site-config";
import { getLatestPost, getPastPosts } from "@/lib/posts";

export default async function Home() {
  const latest = await getLatestPost();
  const past = await getPastPosts();

  if (!latest) return null;

  return (
    <>
      <SiteHeader />

      <main className="flex-1">
        {/* Hero — today's issue */}
        <HeroPost post={latest} />

        {/* Marquee ticker */}
        <Marquee />

        {/* Past issues grid */}
        <section className="mx-auto max-w-7xl px-6 py-16 md:py-24">
          {/* Section header */}
          <div className="flex items-end justify-between gap-6 border-b border-border pb-6">
            <div>
              <p className="kicker text-signal">From the week</p>
              <h2 className="mt-2 font-display text-4xl font-semibold tracking-tight md:text-5xl">Recent Issues</h2>
            </div>
            <Link href="/archive" className="hidden text-sm font-semibold uppercase tracking-[0.2em] underline-grow md:inline-block">
              Browse the archive →
            </Link>
          </div>

          <div className="mt-10 grid gap-x-8 gap-y-14 md:grid-cols-2 lg:grid-cols-3">
            {past.map((post) => (
              <PostCard key={post.id} post={post} />
            ))}
          </div>

          <div className="mt-12 text-center md:hidden">
            <Link href="/archive" className="text-sm font-semibold uppercase tracking-[0.2em] underline-grow">
              Browse the archive →
            </Link>
          </div>
        </section>

        {/* Editor's note */}
        <section className="border-y border-border bg-secondary py-20">
          <div className="mx-auto max-w-4xl px-6 text-center">
            <p className="kicker text-signal">The Editor&apos;s Note</p>
            <blockquote className="mt-6 font-display text-3xl font-medium italic leading-tight tracking-tight md:text-5xl">
              &ldquo;{SITE_CONFIG.editorNote.quote}&rdquo;
            </blockquote>
            <p className="mt-6 text-sm uppercase tracking-[0.22em] text-muted-foreground">
              {SITE_CONFIG.editorNote.attribution}
            </p>
          </div>
        </section>
      </main>

      <SiteFooter />
    </>
  );
}

