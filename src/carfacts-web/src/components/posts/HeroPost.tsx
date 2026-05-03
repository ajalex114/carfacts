import Link from "next/link";
import Image from "next/image";
import type { Post } from "@/lib/types";
import { formatIssueNumber, formatDisplayDate, SITE_CONFIG, getPostLocalHref } from "@/lib/site-config";

interface HeroPostProps {
  post: Post;
}

/**
 * Hero card shown at the top of the home page for today's issue.
 * Left: image (7/12 cols). Right: issue label + large title + CTA.
 */
export default function HeroPost({ post }: HeroPostProps) {
  return (
    <section className="border-b border-border">
      <div className="mx-auto max-w-7xl px-6 py-10 md:py-16">
        {/* "Today's Issue" kicker */}
        <div className="flex items-center gap-3 text-[11px] uppercase tracking-[0.22em] text-signal">
          <span className="inline-block h-2 w-2 animate-pulse rounded-full bg-signal" />
          <span className="font-semibold">Today&apos;s Issue</span>
          <span className="text-muted-foreground">
            /{" "}
            {formatDisplayDate(post.publishedAt)}
          </span>
        </div>

        <div className="mt-8 grid gap-10 md:grid-cols-12 md:gap-12">
          {/* Hero image */}
          <Link
            href={getPostLocalHref(post.publishedAt, post.slug)}
            className="group order-2 block overflow-hidden md:order-1 md:col-span-7"
          >
            <div className="aspect-[5/4] w-full overflow-hidden bg-secondary">
              <Image
                src={post.heroImageUrl}
                alt={post.heroImageAlt}
                width={900}
                height={720}
                priority
                sizes="(max-width: 768px) 100vw, 58vw"
                className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105"
              />
            </div>
          </Link>

          {/* Text */}
          <div className="order-1 md:order-2 md:col-span-5 md:pt-2">
            <p className="kicker text-muted-foreground">
              <span className="text-signal">
                ISSUE NO.&nbsp;{formatIssueNumber(post.issueNumber)}
              </span>
              &nbsp;·&nbsp;{SITE_CONFIG.publicationName}
            </p>

            <h1 className="mt-4 font-display text-5xl font-bold leading-[0.95] tracking-tight md:text-7xl">
              {post.title}
            </h1>

            <p className="mt-6 text-lg leading-relaxed text-muted-foreground md:text-xl">
              {post.subtitle}
            </p>

            <Link
              href={getPostLocalHref(post.publishedAt, post.slug)}
              className="mt-8 inline-flex items-center gap-3 border-b-2 border-foreground pb-1 text-sm font-semibold uppercase tracking-[0.2em] text-foreground transition hover:border-signal hover:text-signal"
            >
              Read today&apos;s 5 facts
              <span aria-hidden="true">→</span>
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
