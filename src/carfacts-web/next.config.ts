import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  images: {
    remotePatterns: [
      {
        protocol: "https",
        hostname: "images.unsplash.com",
      },
      // Production: Azure Blob Storage
      {
        protocol: "https",
        hostname: "*.blob.core.windows.net",
      },
    ],
  },
};

export default nextConfig;
