module.exports = {
  root: true,
  ignorePatterns: ['src/app/routeTree.gen.ts'],
  env: { browser: true, es2022: true, node: true },
  parser: '@typescript-eslint/parser',
  plugins: ['@typescript-eslint', 'boundaries'],
  settings: {
    'import/resolver': {
      typescript: { project: './tsconfig.json' }
    },
    'boundaries/elements': [
      { type: 'app', pattern: ['src/App.tsx', 'src/main.tsx'], mode: 'full' },
      { type: 'route', pattern: 'src/app/**/*', mode: 'full' },
      { type: 'feature-auth', pattern: 'src/components/auth/**/*', mode: 'full' },
      { type: 'feature-contracts', pattern: 'src/components/contracts/**/*', mode: 'full' },
      { type: 'feature-dashboard', pattern: 'src/components/dashboard/**/*', mode: 'full' },
      { type: 'feature-deliveries', pattern: 'src/components/deliveries/**/*', mode: 'full' },
      { type: 'feature-domain', pattern: 'src/components/domain/**/*', mode: 'full' },
      { type: 'feature-market-prices', pattern: 'src/components/market-prices/**/*', mode: 'full' },
      { type: 'shared-ui', pattern: 'src/components/{canvas,grid,layout,ui,visualizations}/**/*', mode: 'full' },
      { type: 'hook', pattern: 'src/hooks/**/*', mode: 'full' },
      { type: 'lib', pattern: 'src/lib/**/*', mode: 'full' },
      { type: 'generated-contract', pattern: 'src/api/generated/**/*', mode: 'full' },
      { type: 'type', pattern: 'src/types/**/*', mode: 'full' },
      { type: 'mock', pattern: 'src/mocks/**/*', mode: 'full' },
      { type: 'worker', pattern: 'src/workers/**/*', mode: 'full' },
      { type: 'style', pattern: 'src/**/*.css', mode: 'full' }
    ]
  },
  rules: {
    'boundaries/no-unknown-files': 2,
    'boundaries/no-unknown': 2,
    'boundaries/element-types': [2, {
      default: 'disallow',
      rules: [
        { from: 'app', allow: ['app', 'route', 'feature-*', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type', 'style'] },
        { from: 'route', allow: ['route', 'feature-*', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type', 'style'] },
        { from: 'feature-auth', allow: ['feature-auth', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
        { from: 'feature-contracts', allow: ['feature-contracts', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
        { from: 'feature-dashboard', allow: ['feature-dashboard', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
        { from: 'feature-deliveries', allow: ['feature-deliveries', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
        { from: 'feature-domain', allow: ['feature-domain', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
        { from: 'feature-market-prices', allow: ['feature-market-prices', 'shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
        { from: 'shared-ui', allow: ['shared-ui', 'hook', 'lib', 'generated-contract', 'type'] },
        { from: 'hook', allow: ['lib', 'generated-contract', 'type', 'worker'] },
        { from: 'lib', allow: ['lib', 'generated-contract', 'type', 'worker'] },
        { from: 'generated-contract', allow: ['generated-contract', 'type'] },
        { from: 'type', allow: ['type', 'generated-contract'] },
        { from: 'mock', allow: ['mock', 'lib', 'generated-contract', 'type'] },
        { from: 'worker', allow: ['lib', 'generated-contract', 'type', 'worker'] }
      ]
    }]
  }
};
