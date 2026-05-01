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
        <section className="border-b border-border">
          <div className="mx-auto max-w-7xl px-6 py-10 md:py-16">
            <div className="grid gap-12 sm:grid-cols-2 lg:grid-cols-3">
              {past.map((post) => (
                <PostCard key={post.id} post={post} />
              ))}
            </div>
          </div>
        </section>

        {/* Editor's note */}
        <section className="mx-auto max-w-7xl px-6 py-12 md:py-16">
          <div className="mx-auto max-w-2xl text-center">
            <p className="kicker mb-4 text-muted-foreground">The Editor&apos;s Note</p>
            <blockquote className="font-display text-2xl font-medium leading-snug tracking-tight text-foreground md:text-3xl">
              {SITE_CONFIG.editorNote.quote}
            </blockquote>
            <p className="mt-4 text-sm text-muted-foreground">
              {SITE_CONFIG.editorNote.attribution}
            </p>
          </div>
        </section>
      </main>

      <SiteFooter />
    </>
  );
}

