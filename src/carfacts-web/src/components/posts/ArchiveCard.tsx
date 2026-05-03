import Link from "next/link";
import Image from "next/image";
import type { Post } from "@/lib/types";
import { formatIssueNumber, formatDisplayDate, getPostLocalHref } from "@/lib/site-config";

interface ArchiveCardProps {
  post: Post;
}

/**
 * Wider card used in the /archive list — image left, text right.
 */
export default function ArchiveCard({ post }: ArchiveCardProps) {
  return (
    <Link
      href={getPostLocalHref(post.publishedAt, post.slug)}
      className="group flex gap-5 border-b border-border py-8 last:border-0"
    >
      {/* Thumbnail */}
      <div className="w-36 shrink-0 overflow-hidden bg-secondary sm:w-48">
        <div className="aspect-[4/3] w-full overflow-hidden">
          <Image
            src={post.heroImageUrl}
            alt={post.heroImageAlt}
            width={400}
            height={300}
            className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-105"
          />
        </div>
      </div>

      {/* Text */}
      <div className="flex flex-col justify-center gap-1">
        <p className="kicker text-signal">
          ISSUE NO.&nbsp;{formatIssueNumber(post.issueNumber)}
        </p>
        <p className="kicker text-muted-foreground">
          {formatDisplayDate(post.publishedAt)}
        </p>

        <h2 className="mt-2 font-display text-xl font-bold leading-tight tracking-tight group-hover:text-signal transition-colors md:text-2xl">
          {post.title}
        </h2>

        <p className="mt-1 text-sm leading-relaxed text-muted-foreground line-clamp-2 hidden sm:block">
          {post.subtitle}
        </p>

        <span className="mt-2 inline-flex items-center gap-1 text-sm font-medium text-signal">
          Read the 5 facts
          <svg
            xmlns="http://www.w3.org/2000/svg"
            width="13"
            height="13"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="transition-transform group-hover:translate-x-0.5"
            aria-hidden="true"
          >
            <path d="M5 12h14" />
            <path d="m12 5 7 7-7 7" />
          </svg>
        </span>
      </div>
    </Link>
  );
}
