import { defineConfig } from 'vite'
import { readFileSync } from "node:fs"

// https://vitejs.dev/config/
export default defineConfig({
  server: {
    https: {
      cert: readFileSync("/home/karan/.aspnet/https/localhost+2.pem"),
      key: readFileSync("/home/karan/.aspnet/https/localhost+2-key.pem"),
    },
    watch: {
      ignored: [
        "**/*.fs" // Don't watch F# files
      ]
    }
  }
})
