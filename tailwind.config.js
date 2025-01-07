import daisyui from 'daisyui';

/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/Ictf.Client/**/*.js"
  ],
  theme: {
    extend: {},
  },
  plugins: [
    daisyui
  ],
}

