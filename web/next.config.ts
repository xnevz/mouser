import type { NextConfig } from "next";

// GitHub Pages serves a static export from https://<user>.github.io/<repo>/; Vercel serves the normal build.
const isGitHubPages = process.env.GITHUB_PAGES === 'true';

const nextConfig: NextConfig = {
  ...(isGitHubPages && { output: 'export', basePath: '/mouser', images: { unoptimized: true } }),
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
