import antfu from '@antfu/eslint-config'

export default antfu({
  type: 'app',
  typescript: true,
  vue: true,
  stylistic: {
    indent: 2,
    quotes: 'single',
    semi: false,
  },
  ignores: [
    '*.md',
    'dist',
    'dist-ssr',
    'node_modules',
    'tsconfig.tsbuildinfo',
  ],
})
