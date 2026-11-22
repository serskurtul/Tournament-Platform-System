import type { Meta, StoryObj } from '@storybook/vue3-vite';
import { fn } from 'storybook/test';
import ConfirmModal from './ConfirmModal.vue';

/**
 * Модальне підтвердження дії. Використовується для скасування участі, дискваліфікації
 * учасника та старту турніру. Під час запиту (`loading`) обидві кнопки вимикаються,
 * щоб дію не було надіслано двічі.
 */
// Props are typed loosely: the build-time tsc sees .vue files through a shim without prop types.
const meta: Meta = {
  title: 'UI/ConfirmModal',
  component: ConfirmModal,
  args: {
    title: 'Скасувати участь?',
    message: 'Ви вийдете з турніру «Spring Cup 2026». Повторна заявка можлива до старту.',
    confirmText: 'Так, вийти',
    cancelText: 'Ні',
    confirmDanger: false,
    loading: false,
    onConfirm: fn(),
    onCancel: fn(),
  },
  argTypes: {
    confirmDanger: { description: 'Червона кнопка для незворотних дій' },
    loading: { description: 'Запит виконується: кнопки disabled, замість тексту "..."' },
  },
};

export default meta;
type Story = StoryObj;

/** Основний стан: звичайне підтвердження. */
export const Default: Story = {};

/** Error/небезпечна дія: незворотна операція з червоною кнопкою. */
export const Danger: Story = {
  args: {
    title: 'Дискваліфікувати учасника?',
    message: 'Учасника «Tigers» буде дискваліфіковано. Скасувати цю дію неможливо.',
    confirmText: 'Так',
    confirmDanger: true,
  },
};

/** Disabled: запит надіслано, повторне натискання заблоковане. */
export const Disabled: Story = {
  args: { loading: true },
};
