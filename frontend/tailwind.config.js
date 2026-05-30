/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts,scss}",
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#ecf3ff',
          100: '#dde9ff',
          200: '#c2d6ff',
          300: '#9cb9ff',
          400: '#7592ff',
          500: '#465fff',
          600: '#3641f5',
          700: '#2a31d8',
          800: '#252dae',
          900: '#262e89',
        },
        // Matching _variables.scss
        primary: {
          DEFAULT: '#465fff', // brand-500
          dark: '#3641f5',    // brand-600
          light: '#dde9ff',   // brand-100
          brand: '#1A91F0',   // The interactive blue from rules
        },
        success: '#34A853',
        warning: '#F59B00',
        error: '#D93025',
        slate: {
          50: '#FAFAFA',
          100: '#F5F5F5',
          200: '#E0E0E0',
          300: '#BDBDBD',
          400: '#9E9E9E',
          500: '#757575',
          600: '#616161',
          700: '#424242',
          800: '#212121',
          900: '#121212',
        }
      },
      fontFamily: {
        outfit: ['Outfit', 'sans-serif'],
        lato: ['Lato', 'sans-serif'],
      },
      borderRadius: {
        'xl': '12px',
        'lg': '8px',
        'md': '4px',
        'sm': '2px',
      }
    },
  },
  plugins: [],
}
