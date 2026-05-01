# DNS Cutover Checklist — WordPress → Azure Static Web App

## T-7 days: Dual Publish begins

- [ ] Azure Function `CarFactsOrchestrator` publishing to both WordPress AND Cosmos DB (dual-publish enabled)
- [ ] Verify Cosmos DB `posts` container is receiving new documents daily
- [ ] Verify Blob Storage `post-images` container is receiving new images daily
- [ ] Verify `web-feeds/feed.xml` and `web-feeds/post-sitemap.xml` are being updated

## T-3 days: SWA validation

- [ ] SWA deployed to Azure (GitHub Action green)
- [ ] SWA custom domain `carfactsdaily.com` added and SSL verified
- [ ] All smoke tests passing on SWA default hostname (`*.azurestaticapps.net`)
- [ ] `robots.txt` accessible and correct
- [ ] `ads.txt` accessible with correct publisher ID
- [ ] RSS feed valid (validate at https://validator.w3.org/feed/)
- [ ] Sitemap valid (validate at https://www.xml-sitemaps.com/validate-xml-sitemap.html)

## T-24 hours: DNS preparation

- [ ] Set DNS TTL to **60 seconds** for `carfactsdaily.com` CNAME record
- [ ] Confirm registrar propagation (use https://dnschecker.org)

## T-0: DNS cutover

- [ ] Update CNAME at registrar: point to `swa-carfacts.azurestaticapps.net`
- [ ] Wait 60 seconds
- [ ] Verify `curl -I https://carfactsdaily.com/` returns 200 from SWA (not WordPress)
- [ ] Verify `curl https://carfactsdaily.com/feed/` returns RSS XML
- [ ] Verify `curl https://carfactsdaily.com/sitemap_index.xml` returns sitemap XML
- [ ] Test 2-3 post URLs manually

## T+24 hours: Analytics & Search

- [ ] Google Search Console: re-submit sitemap `https://carfactsdaily.com/sitemap_index.xml`
- [ ] Bing Webmaster Tools: re-submit sitemap
- [ ] Verify GA4 showing real-time traffic from `carfactsdaily.com`
- [ ] Verify AdSense ads are loading (browser devtools → Network → `pagead`)
- [ ] Check for any 404s in GA4 Site Search or Search Console Coverage

## T+7 days: Disable WordPress publishing

- [ ] Set `WordPress:PostStatus` app setting to `draft` in Function App
  (Posts still go to WP as drafts — not public — while new posts continue to Cosmos/Blob)
- [ ] Or disable `FormatAndPublishActivity` WP path entirely in orchestrator

## T+30 days: Decommission WordPress

- [ ] Export full WordPress backup (just in case)
- [ ] Cancel WordPress.com subscription
- [ ] Remove WordPress OAuth token from Key Vault

## T+30 days: Old subscription cleanup (if migrated)

See: `docs/subscription-migration-runbook.md` Phase 10
