import Link from "next/link";
import { SITE_CONFIG } from "@/lib/site-config";

export default function SiteFooter() {
  return (
    <footer className="border-t border-border bg-background">
      <div className="mx-auto max-w-7xl px-6 py-10 md:py-14">
        {/* Logo */}
        <Link href="/" aria-label={SITE_CONFIG.name}>
          <span className="font-display text-xl font-black tracking-tight">
            {SITE_CONFIG.logoWords[0]}
            <span className="text-signal">.</span>
            {SITE_CONFIG.logoWords[1]}
            <span className="text-signal">.</span>
            {SITE_CONFIG.logoWords[2]}
          </span>
        </Link>

        <p className="mt-3 max-w-sm text-sm text-muted-foreground leading-relaxed">
          {SITE_CONFIG.description}
        </p>

        <div className="mt-8 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-xs text-muted-foreground">
            {SITE_CONFIG.footer.copyright}
          </p>
          <nav className="flex items-center gap-5">
            {SITE_CONFIG.footer.links.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="text-xs text-muted-foreground underline-grow hover:text-foreground transition-colors"
              >
                {link.label}
              </Link>
            ))}
          </nav>
        </div>
      </div>
    </footer>
  );
}
