"""Regenerate sitemaps from Cosmos DB and upload to Blob Storage.

Keeps WordPress-compatible names:
  sitemap.xml        -> index pointing to sitemap-1.xml + image-sitemap-1.xml
  sitemap-1.xml      -> all post URLs with lastmod
  image-sitemap-1.xml -> all post images
"""

import json
import urllib.request
import datetime
import html
import subprocess

# --- Cosmos DB: fetch all posts ---
token_result = subprocess.run(
    ["az", "account", "get-access-token", "--resource", "https://cosmos-carfacts5.documents.azure.com"],
    capture_output=True, text=True, shell=True
)
token = json.loads(token_result.stdout)["accessToken"]

cosmos_url = "https://cosmos-carfacts5.documents.azure.com/dbs/carfacts/colls/posts/docs"
headers = {
    "Authorization": f"type%3Daad%26ver%3D1.0%26sig%3D{token}",
    "x-ms-version": "2018-12-31",
    "x-ms-documentdb-query-enablecrosspartition": "True",
    "Content-Type": "application/query+json",
    "x-ms-documentdb-isquery": "True",
    "x-ms-max-item-count": "1000",
}
body = json.dumps({"query": "SELECT c.slug, c.title, c.postUrl, c.featuredImageUrl, c.publishedAt FROM c"}).encode()
req = urllib.request.Request(cosmos_url, data=body, headers=headers, method="POST")
resp = urllib.request.urlopen(req)
posts = json.loads(resp.read())["Documents"]
posts.sort(key=lambda p: p.get("publishedAt", ""), reverse=True)
print(f"Found {len(posts)} posts")

SITE = "https://carfactsdaily.com"

# --- sitemap-1.xml (post URLs) ---
lines = [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">',
]
for p in posts:
    url = html.escape(p.get("postUrl", ""))
    pub = p.get("publishedAt", "")[:10]
    lines.append("  <url>")
    lines.append(f"    <loc>{url}</loc>")
    lines.append(f"    <lastmod>{pub}</lastmod>")
    lines.append("    <changefreq>monthly</changefreq>")
    lines.append("    <priority>0.8</priority>")
    lines.append("  </url>")
lines.append("</urlset>")
sitemap1 = "\n".join(lines)

# --- image-sitemap-1.xml (post images) ---
img_lines = [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"'
    ' xmlns:image="http://www.google.com/schemas/sitemap-image/1.1">',
]
img_count = 0
for p in posts:
    img = p.get("featuredImageUrl", "")
    if not img:
        continue
    url = html.escape(p.get("postUrl", ""))
    img_count += 1
    img_lines.append("  <url>")
    img_lines.append(f"    <loc>{url}</loc>")
    img_lines.append("    <image:image>")
    img_lines.append(f"      <image:loc>{html.escape(img)}</image:loc>")
    img_lines.append(f"      <image:title>{html.escape(p.get('title', ''))}</image:title>")
    img_lines.append("    </image:image>")
    img_lines.append("  </url>")
img_lines.append("</urlset>")
image_sitemap = "\n".join(img_lines)

# --- sitemap.xml (index) ---
now = datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S+00:00")
sitemap_index = "\n".join([
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">',
    "  <sitemap>",
    f"    <loc>{SITE}/sitemap-1.xml</loc>",
    f"    <lastmod>{now}</lastmod>",
    "  </sitemap>",
    "  <sitemap>",
    f"    <loc>{SITE}/image-sitemap-1.xml</loc>",
    f"    <lastmod>{now}</lastmod>",
    "  </sitemap>",
    "</sitemapindex>",
])

# --- Upload to Blob Storage ---
from azure.storage.blob import BlobServiceClient, ContentSettings

conn_str = (
    "DefaultEndpointsProtocol=https;AccountName=stcarfacts5;"
    "EndpointSuffix=core.windows.net;"
    "AccountKey=<SET_VIA_ENVIRONMENT_VARIABLE>"
)
import os
env_conn = os.environ.get("AZURE_STORAGE_CONNECTION_STRING", "")
if env_conn:
    conn_str = env_conn
blob_client = BlobServiceClient.from_connection_string(conn_str)
container = blob_client.get_container_client("web-feeds")
try:
    container.create_container()
except Exception:
    pass  # already exists
xml_ct = ContentSettings(content_type="application/xml")

container.upload_blob("sitemap-1.xml", sitemap1, overwrite=True, content_settings=xml_ct)
container.upload_blob("image-sitemap-1.xml", image_sitemap, overwrite=True, content_settings=xml_ct)
container.upload_blob("sitemap.xml", sitemap_index, overwrite=True, content_settings=xml_ct)
# Also update sitemap_index.xml to match
container.upload_blob("sitemap_index.xml", sitemap_index, overwrite=True, content_settings=xml_ct)

print(f"Uploaded: sitemap-1.xml ({len(posts)} posts), image-sitemap-1.xml ({img_count} images), sitemap.xml (index)")
print("Done!")
