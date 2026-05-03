import Link from "next/link";
import Image from "next/image";
import type { Post } from "@/lib/types";
import { formatIssueNumber, formatDisplayDate, getPostLocalHref } from "@/lib/site-config";

interface PostCardProps {
  post: Post;
}

/**
 * Card used in the "past issues" grid on the home page.
 */
export default function PostCard({ post }: PostCardProps) {
  return (
    <Link href={getPostLocalHref(post.publishedAt, post.slug)} className="group block animate-fade-up">
      {/* Image */}
      <div className="aspect-[4/3] w-full overflow-hidden bg-secondary">
        <Image
          src={post.heroImageUrl}
          alt={post.heroImageAlt}
          width={600}
          height={450}
          loading="lazy"
          sizes="(max-width: 768px) 100vw, (max-width: 1024px) 50vw, 33vw"
          className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-110"
        />
      </div>

      {/* Meta */}
      <div className="mt-4 flex items-center gap-2 text-[11px] uppercase tracking-[0.18em] text-muted-foreground">
        <span className="text-signal">ISSUE NO.&nbsp;{formatIssueNumber(post.issueNumber)}</span>
        <span>·</span>
        <span>{formatDisplayDate(post.publishedAt)}</span>
      </div>

      <h3 className="mt-2 font-display text-xl font-semibold leading-[1.05] tracking-tight text-foreground transition-colors group-hover:text-signal md:text-2xl">
        {post.title}
      </h3>

      <p className="mt-2 line-clamp-2 text-sm text-muted-foreground md:text-base">
        {post.subtitle}
      </p>
    </Link>
  );
}
