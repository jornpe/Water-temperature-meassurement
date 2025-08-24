import * as React from 'react';
import Header from './Header';
import MainGrid from './MainGrid';

interface SensorsContentProps {
  temperatures?: Array<{
    id: number;
    value: number;
    timestamp: string;
    sensor?: string;
  }>;
}

export default function SensorsContent({ temperatures = [] }: SensorsContentProps) {
  return (
    <>
      <Header />
      <MainGrid temperatures={temperatures} />
    </>
  );
}
