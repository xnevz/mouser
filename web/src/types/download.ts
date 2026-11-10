export type PlatformId = 'macos' | 'windows' | 'linux' | 'android' | 'ios';

export type Architecture = 'arm64' | 'x64' | 'universal' | 'apk' | 'all';

export interface DownloadArtifact {
  title: string;
  filename: string;
  extension: string;
  size: string;
  arch: Architecture;
  archLabel: string;
  recommended?: boolean;
  downloadUrl: string;
  sha256: string;
  notes?: string;
}

export interface PackageManagerOption {
  name: string;
  command: string;
  description: string;
}

export interface PlatformRelease {
  id: PlatformId;
  name: string;
  badge: string;
  iconName: string;
  subtitle: string;
  supportedVersions: string;
  latestVersion: string;
  releaseDate: string;
  primaryArtifact: DownloadArtifact;
  artifacts: DownloadArtifact[];
  packageManagers: PackageManagerOption[];
  quickInstructions: string[];
}
