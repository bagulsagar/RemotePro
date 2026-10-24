import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const adminKey = env.REMOTEPRO_ADMIN_KEY

  return {
    plugins: [react()],
    server: {
      proxy: {
        '/api': {
          target: 'http://localhost:5150',
          changeOrigin: true,
          configure: (proxy) => {
            proxy.on('proxyReq', (proxyReq, req) => {
              if (req.url?.startsWith('/api/devices')) {
                if (!adminKey) {
                  proxyReq.destroy(
                    new Error('REMOTEPRO_ADMIN_KEY is not configured')
                  )
                  return
                }

                proxyReq.setHeader('X-Admin-Key', adminKey)
              }
            })
          },
        },
      },
    },
  }
})
