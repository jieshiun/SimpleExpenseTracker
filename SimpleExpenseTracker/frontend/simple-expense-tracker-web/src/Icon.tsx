export type IconName = 'home' | 'list' | 'chart' | 'settings' | 'plus' | 'left' | 'right' | 'close' | 'wallet';
const paths: Record<IconName, string> = {
  home: 'm3 10 9-7 9 7v10a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1Z',
  list: 'M8 5h13M8 12h13M8 19h13M3 5h.01M3 12h.01M3 19h.01',
  chart: 'M4 20V10M12 20V4M20 20v-7', settings: 'M4 7h16M4 17h16M9 4v6M16 14v6',
  plus: 'M12 5v14M5 12h14', left: 'm14 6-6 6 6 6', right: 'm10 6 6 6-6 6', close: 'm6 6 12 12M18 6 6 18', wallet: 'M20 7H4V4h14v3M4 7v13h17V7ZM16 12h5v4h-5Z',
};
export default function Icon({ name, size = 22 }: { name: IconName; size?: number }) { return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name]} /></svg>; }
