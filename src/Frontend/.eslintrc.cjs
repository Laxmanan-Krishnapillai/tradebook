module.exports = {
  root: true,
  env: { browser: true, es2022: true, node: true },
  parser: '@typescript-eslint/parser',
  plugins: ['@typescript-eslint', 'boundaries'],
  settings: {
    'boundaries/elements': [
      { type: 'ui', pattern: 'src/components/ui/*' },
      { type: 'feature', pattern: 'src/features/*' },
      { type: 'store', pattern: 'src/store/*' },
      { type: 'types', pattern: ['src/types/*', 'src/api/*'] },
      { type: 'lib', pattern: 'src/lib/*' }
    ]
  },
  rules: {
    'boundaries/entry-point': [2, { default: 'disallow', rules: [{ target: 'ui', allow: 'index.ts' }] }],
    'boundaries/element-types': [2, {
      default: 'disallow',
      rules: [
        { from: 'feature', allow: ['ui', 'store', 'types', 'lib'] },
        { from: 'ui', allow: ['types', 'lib'] },
        { from: 'store', allow: ['types', 'lib'] },
        { from: 'lib', allow: ['types'] }
      ]
    }]
  }
};
