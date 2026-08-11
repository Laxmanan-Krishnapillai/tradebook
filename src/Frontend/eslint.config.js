// @ts-check
import js from '@eslint/js';
import vitest from '@vitest/eslint-plugin';
import boundaries from 'eslint-plugin-boundaries';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import reactHooks from 'eslint-plugin-react-hooks';
import testingLibrary from 'eslint-plugin-testing-library';
import tailwindcss from 'eslint-plugin-tailwindcss';
import globals from 'globals';
import tseslint from 'typescript-eslint';

// Design-system classnames living in the plain CSS layers (src/styles.css,
// src/components/canvas/WorkflowCanvas.css) plus the ui-*/kpi-* component hooks.
// eslint-plugin-tailwindcss anchors every entry as ^entry$, so plain strings match
// exactly and the prefix entries below use `prefix-.*` (not `^prefix-`).
const designSystemWhitelist = [
  // density utility applied by the density toggle
  'u-density-override',
  // component-scoped classname prefixes (ui primitives, KPI tiles, workflow canvas)
  'ui-.*',
  'kpi-.*',
  'workflow-.*',
  // app shell + page scaffolding classes defined in src/styles.css
  'app-shell',
  'workspace',
  'page-header',
  'toolbar',
  'row-actions',
  'login-shell',
  'login-card',
  'modal',
  'error-banner',
  'live-status',
  'eyebrow',
  'dashboard-grid',
  'reduce-motion',
  // button variants styled through base-layer element selectors in src/styles.css
  'secondary',
  'danger',
  // tokens referenced from markup that still need @theme definitions (task-23 debt)
  'rounded-card',
  'border-brand-600',
];

export default tseslint.config(
  { ignores: ['dist/**', 'src/api/generated/**', 'src/app/routeTree.gen.ts'] },
  js.configs.recommended,
  ...tseslint.configs.recommendedTypeChecked,
  {
    files: ['src/**/*.{ts,tsx}', 'tests/**/*.{ts,tsx}'],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
      globals: { ...globals.browser, ...globals.node },
    },
    plugins: {
      boundaries,
      tailwindcss,
      'jsx-a11y': jsxA11y,
      'react-hooks': reactHooks,
    },
    settings: {
      'import/resolver': { typescript: { alwaysTryTypes: true } },
      'jsx-a11y': {
        components: { Input: 'input', NumberInput: 'input' },
      },
      tailwindcss: {
        cssConfigPath: './src/styles.css',
        functions: ['cva', 'cx', 'cn', 'tv'],
        // Kept in sync with the no-custom-classname rule options below; the
        // installed plugin (v4.2.0) only reads the whitelist from rule options.
        whitelist: designSystemWhitelist,
      },
      'boundaries/elements': [
        { type: 'feature', pattern: 'src/features/**/*', mode: 'full' },
        { type: 'app', pattern: ['src/App.tsx', 'src/main.tsx'], mode: 'full' },
        { type: 'route', pattern: 'src/app/**/*', mode: 'full' },
        { type: 'feature-auth', pattern: 'src/components/auth/**/*', mode: 'full' },
        { type: 'feature-contracts', pattern: 'src/components/contracts/**/*', mode: 'full' },
        { type: 'feature-dashboard', pattern: 'src/components/dashboard/**/*', mode: 'full' },
        { type: 'feature-deliveries', pattern: 'src/components/deliveries/**/*', mode: 'full' },
        { type: 'feature-domain', pattern: 'src/components/domain/**/*', mode: 'full' },
        { type: 'feature-market-prices', pattern: 'src/components/market-prices/**/*', mode: 'full' },
        { type: 'providers', pattern: 'src/components/providers/**/*', mode: 'full' },
        { type: 'shared-ui', pattern: 'src/components/{canvas,grid,kpi,layout,ui,visualizations}/**/*', mode: 'full' },
        { type: 'hook', pattern: 'src/hooks/**/*', mode: 'full' },
        { type: 'stores', pattern: 'src/stores/**/*', mode: 'full' },
        { type: 'lib', pattern: 'src/lib/**/*', mode: 'full' },
        { type: 'generated-contract', pattern: 'src/api/generated/**/*', mode: 'full' },
        { type: 'type', pattern: 'src/types/**/*', mode: 'full' },
        { type: 'mock', pattern: 'src/mocks/**/*', mode: 'full' },
        { type: 'worker', pattern: 'src/workers/**/*', mode: 'full' },
        { type: 'style', pattern: 'src/**/*.css', mode: 'full' },
      ],
    },
    rules: {
      'tailwindcss/no-arbitrary-value': 'error',
      'tailwindcss/no-custom-classname': ['error', { whitelist: designSystemWhitelist }],
      ...jsxA11y.flatConfigs.recommended.rules,
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'warn',
      '@typescript-eslint/no-floating-promises': 'error',
      '@typescript-eslint/no-misused-promises': 'error',
      'react-hooks/config': 'error',
      '@typescript-eslint/no-base-to-string': 'off',
      '@typescript-eslint/no-explicit-any': 'off',
      '@typescript-eslint/no-unnecessary-type-assertion': 'off',
      '@typescript-eslint/no-unused-vars': 'off',
      '@typescript-eslint/only-throw-error': 'off',
      'boundaries/no-unknown-files': 'error',
      'boundaries/no-unknown': 'error',
      'boundaries/element-types': ['error', {
        default: 'disallow',
        rules: [
          { from: 'app', allow: ['app', 'route', 'feature-*', 'providers', 'shared-ui', 'stores', 'hook', 'lib', 'generated-contract', 'type', 'style'] },
          { from: 'route', allow: ['route', 'feature-*', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type', 'style'] },
          { from: 'feature-auth', allow: ['feature-auth', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
          { from: 'feature-contracts', allow: ['feature-contracts', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
          { from: 'feature-dashboard', allow: ['feature-dashboard', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
          { from: 'feature-deliveries', allow: ['feature-deliveries', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
          { from: 'feature-domain', allow: ['feature-domain', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
          { from: 'feature-market-prices', allow: ['feature-market-prices', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
          { from: 'providers', allow: ['providers', 'stores', 'hook', 'lib', 'generated-contract', 'type'] },
          { from: 'shared-ui', allow: ['shared-ui', 'stores', 'hook', 'lib', 'generated-contract', 'type'] },
          { from: 'hook', allow: ['lib', 'generated-contract', 'type', 'worker'] },
          { from: 'stores', allow: ['stores', 'lib', 'generated-contract', 'type'] },
          { from: 'lib', allow: ['lib', 'generated-contract', 'type', 'worker'] },
          { from: 'generated-contract', allow: ['generated-contract', 'type'] },
          { from: 'type', allow: ['type', 'generated-contract'] },
          { from: 'mock', allow: ['mock', 'lib', 'generated-contract', 'type'] },
          { from: 'worker', allow: ['lib', 'generated-contract', 'type', 'worker'] },
        ],
      }],
      'boundaries/external': ['error', {
        default: 'allow',
        rules: [{
          from: ['feature'],
          disallow: ['@base-ui-components/*'],
          message: 'Compose @/components/ui/* — do not hand-roll a Base UI primitive the registry ships.',
        }],
      }],
    },
  },
  {
    files: ['tests/**/*.{test,spec}.{ts,tsx}', 'src/**/*.{test,spec}.{ts,tsx}', 'tests/setup.ts'],
    plugins: { vitest, 'testing-library': testingLibrary },
    rules: {
      ...vitest.configs.recommended.rules,
      ...testingLibrary.configs['flat/react'].rules,
    },
  },
);
