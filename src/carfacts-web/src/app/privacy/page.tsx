import type { Metadata } from "next";
import SiteHeader from "@/components/layout/SiteHeader";
import SiteFooter from "@/components/layout/SiteFooter";
import { SITE_CONFIG } from "@/lib/site-config";

export const metadata: Metadata = {
  title: "Privacy Policy",
  description: `Privacy Policy for ${SITE_CONFIG.name}.`,
};

export default function PrivacyPage() {
  return (
    <>
      <SiteHeader />

      <main className="flex-1">
        <div className="mx-auto max-w-3xl px-6 py-10 md:py-16">
          <header className="border-b border-border pb-10">
            <p className="kicker text-signal mb-3">Legal</p>
            <h1 className="font-display text-5xl font-bold tracking-tight leading-[0.95] md:text-7xl">
              Privacy Policy
            </h1>
          </header>

          <div className="mt-10 space-y-8 text-base leading-relaxed text-muted-foreground md:text-lg">
            <p className="text-sm text-muted-foreground">Last updated: May 2026</p>

            <section className="space-y-3">
              <h2 className="font-display text-2xl font-bold text-foreground">What we collect</h2>
              <p>
                {SITE_CONFIG.name} does not collect personal information directly. We use
                third-party services that may collect anonymised usage data:
              </p>
              <ul className="list-disc pl-6 space-y-1">
                <li><strong className="text-foreground">Google Analytics</strong> — page views and session data (anonymised IP)</li>
                <li><strong className="text-foreground">Google AdSense</strong> — ad impressions and clicks to serve relevant ads</li>
              </ul>
            </section>

            <section className="space-y-3">
              <h2 className="font-display text-2xl font-bold text-foreground">Cookies</h2>
              <p>
                Third-party services (Google Analytics, AdSense) may set cookies in your browser.
                You can opt out via your browser settings or{" "}
                <a
                  href="https://tools.google.com/dlpage/gaoptout"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-signal underline-grow"
                >
                  Google&apos;s opt-out tool
                </a>.
              </p>
            </section>

            <section className="space-y-3">
              <h2 className="font-display text-2xl font-bold text-foreground">Data sharing</h2>
              <p>
                We do not sell or share your personal data. Anonymised analytics data is processed
                by Google under their own privacy policy.
              </p>
            </section>
          </div>
        </div>
      </main>

      <SiteFooter />
    </>
  );
}
