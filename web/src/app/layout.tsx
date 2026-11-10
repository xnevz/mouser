import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Mouser — Cross-Platform Virtual KVM & Smart Clipboard",
  description:
    "Seamlessly glide mouse and keyboard between macOS, Windows, Linux, and Android with <3ms latency and zero-knowledge encrypted clipboard synchronization.",
  keywords: [
    "Virtual KVM",
    "ShareMouse alternative",
    "Synergy alternative",
    "Universal Control",
    "Clipboard Sync",
    "Cross-platform mouse sharing",
    "Zero latency KVM",
  ],
  authors: [{ name: "Mouser Open Source Contributors" }],
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="en"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">{children}</body>
    </html>
  );
}
