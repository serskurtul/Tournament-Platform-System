// ESLint 9+/10 flat config (replaces the legacy .eslintrc.json, which ESLint 10 no longer reads).
import js from '@eslint/js';
import globals from 'globals';
import vueParser from 'vue-eslint-parser';
import tsParser from '@typescript-eslint/parser';
import tsPlugin from '@typescript-eslint/eslint-plugin';
import vuePlugin from 'eslint-plugin-vue';
import prettier from 'eslint-config-prettier';

export default [
    { ignores: ['dist/**', 'node_modules/**', 'storybook-static/**'] },
    js.configs.recommended,
    ...vuePlugin.configs['flat/recommended'],
    {
        files: ['**/*.{ts,js,vue}'],
        languageOptions: {
            parser: vueParser,
            parserOptions: {
                parser: tsParser,
                ecmaVersion: 'latest',
                sourceType: 'module',
                extraFileExtensions: ['.vue'],
            },
            globals: { ...globals.browser, ...globals.node },
        },
        plugins: { '@typescript-eslint': tsPlugin },
        rules: {
            ...tsPlugin.configs.recommended.rules,
            semi: ['error', 'always'],
            quotes: ['error', 'single'],
            'max-len': ['warn', { code: 100 }],
            'vue/multi-word-component-names': 'off',
            // A leading underscore marks an intentionally unused argument (e.g. interface stubs).
            '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
        },
    },
    prettier,
];
