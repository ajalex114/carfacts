import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "export",
  trailingSlash: true,
  images: {
    // Static export doesn't support Next.js image optimization server
    unoptimized: true,
    remotePatterns: [
      {
        protocol: "https",
        hostname: "images.unsplash.com",
      },
      {
        protocol: "https",
        hostname: "i0.wp.com",
      },
      {
        protocol: "https",
        hostname: "carfactsdaily.com",
      },
      {
        protocol: "https",
        hostname: "stblobcarfacts5.blob.core.windows.net",
      },
    ],
  },
};

export default nextConfig;
