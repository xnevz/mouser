export type PlatformId = 'windows' | 'macos' | 'linux' | 'android';

export interface PlatformInfo {
  id: PlatformId;
  name: string;
  /** Short state shown on the tab, e.g. "Works today". */
  badge: string;
  summary: string;
  requirements: string;
  works: string[];
  notYet: string[];
  /** Command run from the repository root. */
  buildCommand: string;
  buildOutput: string;
  setup: string[];
}
