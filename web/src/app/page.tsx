import { Navbar } from '@/components/Navbar';
import { Hero } from '@/components/Hero';
import { InteractiveCanvasDemo } from '@/components/InteractiveCanvasDemo';
import { DownloadSection } from '@/components/DownloadSection';
import { FeatureGrid } from '@/components/FeatureGrid';
import { ArchitecturePresentation } from '@/components/ArchitecturePresentation';
import { ComparisonTable } from '@/components/ComparisonTable';
import { Footer } from '@/components/Footer';

export default function Home() {
  return (
    <div className="min-h-screen bg-[#060911] text-slate-100 flex flex-col font-sans selection:bg-cyan-500 selection:text-white">
      <Navbar />
      <main className="flex-1">
        <Hero />
        <InteractiveCanvasDemo />
        <DownloadSection />
        <FeatureGrid />
        <ArchitecturePresentation />
        <ComparisonTable />
      </main>
      <Footer />
    </div>
  );
}
