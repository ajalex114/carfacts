import Link from "next/link";
import Image from "next/image";
import { MockPost } from "@/lib/mock-data";
import { formatIssueNumber, formatDisplayDate } from "@/lib/site-config";

interface PostCardProps {
  post: MockPost;
}

/**
 * Card used in the "past issues" grid on the home page.
 */
export default function PostCard({ post }: PostCardProps) {
  return (
    <Link href={post.postUrl} className="group block">
      {/* Image */}
      <div className="aspect-[16/10] w-full overflow-hidden bg-secondary">
        <Image
          src={post.heroImageUrl}
          alt={post.heroImageAlt}
          width={800}
          height={500}
          className="ken-burns h-full w-full object-cover"
        />
      </div>

      {/* Meta */}
      <div className="mt-4">
        <p className="kicker text-muted-foreground">
          <span className="text-signal">
            ISSUE NO.&nbsp;{formatIssueNumber(post.issueNumber)}
          </span>
          &nbsp;·&nbsp;{formatDisplayDate(post.publishedAt)}
        </p>

        <h3 className="mt-2 font-display text-2xl font-bold leading-tight tracking-tight group-hover:text-signal transition-colors md:text-3xl">
          {post.title}
        </h3>

        <p className="mt-2 text-sm leading-relaxed text-muted-foreground line-clamp-2">
          {post.subtitle}
        </p>

        <span className="mt-3 inline-flex items-center gap-1 text-sm font-medium text-signal">
          Read the 5 facts
          <svg
            xmlns="http://www.w3.org/2000/svg"
            width="14"
            height="14"
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
