import { notFound } from "next/navigation";
import type { Metadata } from "next";
import Link from "next/link";
import Image from "next/image";
import SiteHeader from "@/components/layout/SiteHeader";
import SiteFooter from "@/components/layout/SiteFooter";
import { getMockPostByParams, MOCK_POSTS } from "@/lib/mock-data";
import { formatIssueNumber, formatDisplayDate, SITE_CONFIG } from "@/lib/site-config";

interface PostPageProps {
  params: Promise<{
    year: string;
    month: string;
    day: string;
    slug: string;
  }>;
}

export async function generateStaticParams() {
  return MOCK_POSTS.map((post) => {
    const [year, month, day] = post.publishedAt.split("T")[0].split("-");
    return { year, month, day, slug: post.slug };
  });
}

export async function generateMetadata({ params }: PostPageProps): Promise<Metadata> {
  const { year, month, day, slug } = await params;
  const post = getMockPostByParams(year, month, day, slug);
  if (!post) return {};

  return {
    title: post.title,
    description: post.subtitle,
    alternates: { canonical: post.postUrl },
    openGraph: {
      title: post.title,
      description: post.subtitle,
      images: [{ url: post.heroImageUrl, alt: post.heroImageAlt }],
      type: "article",
      publishedTime: post.publishedAt,
    },
    twitter: {
      card: "summary_large_image",
      title: post.title,
      description: post.subtitle,
      images: [post.heroImageUrl],
    },
  };
}

export default async function PostPage({ params }: PostPageProps) {
  const { year, month, day, slug } = await params;
  const post = getMockPostByParams(year, month, day, slug);
  if (!post) notFound();

  return (
    <>
      <SiteHeader />

      <main className="flex-1">
        <article className="mx-auto max-w-3xl px-6 py-10 md:py-16">
          {/* Breadcrumb */}
          <nav className="flex items-center gap-2 kicker text-muted-foreground mb-8">
            <Link href="/archive" className="underline-grow hover:text-foreground transition-colors">
              ← Archive
            </Link>
            <span>/</span>
            <span className="text-signal">
              ISSUE NO.&nbsp;{formatIssueNumber(post.issueNumber)}
            </span>
            <span>·</span>
            <span>{formatDisplayDate(post.publishedAt)}</span>
          </nav>

          {/* Title block */}
          <header>
            <h1 className="font-display text-4xl font-bold leading-tight tracking-tight md:text-6xl">
              {post.title}
            </h1>
            <p className="mt-4 text-lg leading-relaxed text-muted-foreground md:text-xl">
              {post.subtitle}
            </p>
          </header>

          {/* Hero image */}
          <div className="mt-8 aspect-[16/9] w-full overflow-hidden bg-secondary">
            <Image
              src={post.heroImageUrl}
              alt={post.heroImageAlt}
              width={1600}
              height={900}
              priority
              className="h-full w-full object-cover"
            />
          </div>

          {/* Intro */}
          <p className="mt-8 text-base leading-relaxed text-muted-foreground md:text-lg">
            {post.intro}
          </p>

          {/* Five Facts */}
          <section className="mt-12">
            <div className="mb-8 border-t border-border pt-8">
              <p className="kicker text-signal mb-1">The Five Facts</p>
              <h2 className="font-display text-3xl font-bold tracking-tight md:text-4xl">
                What we learned today.
              </h2>
            </div>

            <ol className="space-y-16">
              {post.facts.map((fact, index) => (
                <li key={index} className="grid gap-6 md:grid-cols-12 md:gap-10">
                  {/* Fact number */}
                  <div className="md:col-span-1">
                    <span className="font-display text-5xl font-black text-border leading-none">
                      {String(index + 1).padStart(2, "0")}
                    </span>
                  </div>

                  {/* Fact content */}
                  <div className="md:col-span-11">
                    <h3 className="font-display text-2xl font-bold tracking-tight leading-snug mb-3 md:text-3xl">
                      {fact.title}
                    </h3>
                    <p className="text-base leading-relaxed text-muted-foreground md:text-lg">
                      {fact.body}
                    </p>

                    {/* Fact image */}
                    <figure className="mt-6">
                      <div className="aspect-[16/9] w-full overflow-hidden bg-secondary">
                        <Image
                          src={fact.imageUrl}
                          alt={fact.imageAlt}
                          width={1200}
                          height={675}
                          className="h-full w-full object-cover"
                        />
                      </div>
                      <figcaption className="mt-2 text-xs text-muted-foreground">
                        {fact.imageAlt}
                      </figcaption>
                    </figure>
                  </div>
                </li>
              ))}
            </ol>
          </section>

          {/* Share / navigation */}
          <div className="mt-16 border-t border-border pt-8 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <Link
              href="/archive"
              className="inline-flex items-center gap-2 text-sm font-medium text-muted-foreground underline-grow hover:text-foreground transition-colors"
            >
              ← Back to archive
            </Link>
            <p className="kicker text-muted-foreground">
              {SITE_CONFIG.name} · Issue {formatIssueNumber(post.issueNumber)}
            </p>
          </div>
        </article>
      </main>

      <SiteFooter />
    </>
  );
}
