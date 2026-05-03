import Link from "next/link";
import { SITE_CONFIG } from "@/lib/site-config";

export default function SiteFooter() {
  const logoWords = SITE_CONFIG.logoWords;

  return (
    <footer className="mt-24 border-t border-border bg-secondary">
      <div className="mx-auto max-w-7xl px-6 py-14">
        <div className="grid gap-10 md:grid-cols-3">
          {/* Brand */}
          <div>
            <Link href="/" aria-label={SITE_CONFIG.name}>
              <p className="font-display text-3xl font-black tracking-tight">
                {logoWords[0]}
                <span className="text-signal">.</span>
                {logoWords[1]}
                <span className="text-signal">.</span>
                {logoWords[2]}
              </p>
            </Link>
            <p className="mt-3 max-w-xs text-sm text-muted-foreground">
              {SITE_CONFIG.description}
            </p>
          </div>

          {/* Sections */}
          <div>
            <p className="kicker text-muted-foreground">Sections</p>
            <ul className="mt-4 space-y-2 text-sm">
              <li><Link href="/" className="underline-grow">Today&apos;s Issue</Link></li>
              <li><Link href="/archive" className="underline-grow">Archive</Link></li>
              <li><Link href="/stories" className="underline-grow">Stories</Link></li>
              <li><Link href="/about" className="underline-grow">About</Link></li>
            </ul>
          </div>

          {/* Social */}
          <div>
            <p className="kicker text-muted-foreground">Follow the road</p>
            <ul className="mt-4 space-y-2 text-sm">
              {SITE_CONFIG.footer.links.map((link) => (
                <li key={link.href}>
                  <Link href={link.href} className="underline-grow">
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        </div>

        {/* Bottom bar */}
        <div className="mt-12 flex flex-col items-start justify-between gap-3 border-t border-border pt-6 text-xs uppercase tracking-[0.18em] text-muted-foreground md:flex-row md:items-center">
          <span>{SITE_CONFIG.footer.copyright}</span>
          <span>Printed on the internet</span>
        </div>
      </div>
    </footer>
  );
}
