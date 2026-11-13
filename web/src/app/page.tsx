import { Navbar } from '@/components/Navbar';
import { Hero } from '@/components/Hero';
import { DownloadSection } from '@/components/DownloadSection';
import { FeatureGrid } from '@/components/FeatureGrid';
import { ArchitecturePresentation } from '@/components/ArchitecturePresentation';
import { Footer } from '@/components/Footer';
import { InteractiveMouseFollower } from '@/components/InteractiveMouseFollower';

export default function Home() {
  return (
    <div className="min-h-screen bg-mesh-matrix text-slate-900 dark:text-slate-100 flex flex-col font-sans selection:bg-cyan-500 selection:text-white relative transition-colors duration-200">
      <Navbar />
      <main className="flex-1 relative z-10">
        <Hero />
        <FeatureGrid />
        <DownloadSection />
        <ArchitecturePresentation />
      </main>
      <Footer />
      <InteractiveMouseFollower />
    </div>
  );
}
