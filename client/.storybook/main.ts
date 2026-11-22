import type { StorybookConfig } from '@storybook/vue3-vite';

// ЛР-11 (варіативний пункт): жива документація Design System. Запуск: npm run storybook
const config: StorybookConfig = {
  stories: ['../src/**/*.stories.ts'],
  framework: { name: '@storybook/vue3-vite', options: {} },
  docs: { defaultName: 'Документація' },
};

export default config;
