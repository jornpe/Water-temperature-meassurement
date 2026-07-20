import * as React from 'react';
import Header from './Header';
import MainGrid from './MainGrid';
import type { DeviceSummary } from '../api';

interface SensorsContentProps {
  devices: DeviceSummary[];
  onSelectDevice: (device: DeviceSummary) => void;
}

export default function SensorsContent({ devices, onSelectDevice }: SensorsContentProps) {
  return (
    <>
      <Header />
      <MainGrid devices={devices} onSelectDevice={onSelectDevice} />
    </>
  );
}
