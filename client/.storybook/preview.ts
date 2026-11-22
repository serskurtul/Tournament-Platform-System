import '../src/style.css';
import type { Preview } from '@storybook/vue3-vite';

const preview: Preview = {
  tags: ['autodocs'],
  parameters: {
    layout: 'fullscreen',
    controls: { expanded: true },
  },
};

export default preview;
