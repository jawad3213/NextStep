/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts,scss}",
  ],
  theme: {
    extend: {
      colors: {
        // Matching _variables.scss
        primary: {
          DEFAULT: '#0C1986',
          dark: '#091361',
          light: '#E7E8F3',
          brand: '#1A91F0', // The interactive blue from rules
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
