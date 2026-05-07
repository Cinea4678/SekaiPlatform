import process from 'node:process'
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiProxyTarget = env.API_PROXY_TARGET || 'https://platform.pjs.accr.cc'

  return {
    base: '/',
    plugins: [vue(), tailwindcss()],
    server: {
      host: '0.0.0.0',
      port: 5000,
      hmr: {
        clientPort: 443,
      },
      proxy: {
        '/api': {
          target: apiProxyTarget,
          changeOrigin: true,
          secure: true,
          cookieDomainRewrite: '',
        },
      },
    },
    preview: {
      host: '0.0.0.0',
      port: 5000,
    },
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
  }
})
