import React, { useEffect, useRef } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import {
  Box,
  Container,
  CssBaseline,
  ThemeProvider,
  createTheme,
  Typography,
  Paper,
  useMediaQuery,
} from '@mui/material';
import { blue, deepPurple, indigo, purple } from '@mui/material/colors';
import LoginForm from '../components/auth/LoginForm';

// Animated background component
const AnimatedBackground = () => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    
    // Set canvas size
    const resizeCanvas = () => {
      canvas.width = window.innerWidth;
      canvas.height = window.innerHeight;
    };
    
    window.addEventListener('resize', resizeCanvas);
    resizeCanvas();
    
    // Particles configuration
    const particles: {x: number, y: number, size: number, speedX: number, speedY: number}[] = [];
    const particleCount = Math.floor((window.innerWidth * window.innerHeight) / 15000);
    
    // Create particles
    for (let i = 0; i < particleCount; i++) {
      particles.push({
        x: Math.random() * canvas.width,
        y: Math.random() * canvas.height,
        size: Math.random() * 2 + 1,
        speedX: Math.random() * 0.5 - 0.25,
        speedY: Math.random() * 0.5 - 0.25
      });
    }
    
    // Animation loop
    let animationFrameId: number;
    const animate = () => {
      if (!ctx) return;
      
      // Create gradient background
      const gradient = ctx.createLinearGradient(0, 0, window.innerWidth, window.innerHeight);
      gradient.addColorStop(0, indigo[900]);
      gradient.addColorStop(0.5, purple[900]);
      gradient.addColorStop(1, indigo[800]);
      
      // Clear and fill with gradient
      ctx.fillStyle = gradient;
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      
      // Draw and update particles
      particles.forEach(particle => {
        // Update position
        particle.x += particle.speedX;
        particle.y += particle.speedY;
        
        // Bounce off edges
        if (particle.x < 0 || particle.x > canvas.width) particle.speedX *= -1;
        if (particle.y < 0 || particle.y > canvas.height) particle.speedY *= -1;
        
        // Draw particle
        ctx.beginPath();
        ctx.arc(particle.x, particle.y, particle.size, 0, Math.PI * 2);
        ctx.fillStyle = `rgba(255, 255, 255, ${particle.size / 3})`;
        ctx.fill();
      });
      
      // Draw connecting lines
      for (let i = 0; i < particles.length; i++) {
        for (let j = i + 1; j < particles.length; j++) {
          const dx = particles[i].x - particles[j].x;
          const dy = particles[i].y - particles[j].y;
          const distance = Math.sqrt(dx * dx + dy * dy);
          
          if (distance < 100) {
            ctx.beginPath();
            ctx.strokeStyle = `rgba(255, 255, 255, ${1 - distance / 100})`;
            ctx.lineWidth = 0.5;
            ctx.moveTo(particles[i].x, particles[i].y);
            ctx.lineTo(particles[j].x, particles[j].y);
            ctx.stroke();
          }
        }
      }
      
      animationFrameId = requestAnimationFrame(animate);
    };
    
    animate();
    
    // Cleanup
    return () => {
      cancelAnimationFrame(animationFrameId);
      window.removeEventListener('resize', resizeCanvas);
    };
  }, []);
  
  return (
    <canvas 
      ref={canvasRef} 
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        width: '100%',
        height: '100%',
        zIndex: 0,
      }}
    />
  );
};

const theme = createTheme({
  palette: {
    primary: {
      main: deepPurple[700],
      light: deepPurple[500],
      dark: deepPurple[900],
      contrastText: '#fff',
    },
    secondary: {
      main: '#7c4dff',
      light: '#b47cff',
      dark: '#3f1dcb',
      contrastText: '#fff',
    },
    background: {
      default: '#f8f9fa',
      paper: '#ffffff',
    },
  },
  typography: {
    fontFamily: '"Poppins", "Roboto", "Helvetica", "Arial", sans-serif',
    h4: {
      fontWeight: 600,
      color: deepPurple[900],
    },
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: 'none',
          fontWeight: 500,
          borderRadius: 8,
          padding: '10px 24px',
          boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)',
          '&:hover': {
            boxShadow: '0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)',
          },
        },
      },
    },
    MuiTextField: {
      styleOverrides: {
        root: {
          '& .MuiOutlinedInput-root': {
            borderRadius: 8,
            '& fieldset': {
              borderColor: '#e2e8f0',
            },
            '&:hover fieldset': {
              borderColor: deepPurple[300],
            },
            '&.Mui-focused fieldset': {
              borderColor: deepPurple[500],
              borderWidth: 1,
            },
          },
        },
      },
    },
  },
  shape: {
    borderRadius: 12,
  },
});

const LoginPage: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const from = location.state?.from?.pathname || '/';

  const handleLoginSuccess = () => {
    navigate(from, { replace: true });
  };

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <AnimatedBackground />
      <Box
        sx={{
          minHeight: '100vh',
          display: 'flex',
          flexDirection: 'column',
          position: 'relative',
          overflow: 'hidden',
          '&::before': {
            content: '""',
            position: 'absolute',
            width: '100%',
            height: '100%',
            background: 'linear-gradient(135deg, rgba(26, 35, 126, 0.85) 0%, rgba(74, 20, 140, 0.85) 100%)',
            zIndex: 1,
          },
        }}
      >
        <Container 
          component="main" 
          maxWidth="md" 
          sx={{
            display: 'flex',
            flexDirection: { xs: 'column', md: 'row' },
            alignItems: 'center',
            justifyContent: 'center',
            flex: 1,
            py: 6,
            position: 'relative',
            zIndex: 1,
          }}
        >
          {/* Left side - Welcome Content */}
          <Box
            sx={{
              flex: 1,
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'center',
              alignItems: { xs: 'center', md: 'flex-end' },
              textAlign: { xs: 'center', md: 'right' },
              pr: { md: 8 },
              mb: { xs: 6, md: 0 },
              position: 'relative',
              zIndex: 2,
            }}
          >
            <Box sx={{ maxWidth: 480 }}>
              <Typography 
                variant="h3" 
                component="h1" 
                sx={{
                  fontWeight: 800,
                  mb: 2,
                  color: '#fff',
                  textShadow: '0 2px 10px rgba(0,0,0,0.2)',
                }}
              >
                Welcome Back!
              </Typography>
              <Typography 
                variant="h6" 
                color="text.secondary"
                sx={{
                  mb: 3,
                  opacity: 0.9,
                }}
              >
                Sign in to access your Maof POS account
              </Typography>
              <Box 
                component="img"
                src="/assets/images/login-illustration.svg"
                alt="Login Illustration"
                sx={{
                  width: '100%',
                  maxWidth: 400,
                  height: 'auto',
                  display: { xs: 'none', md: 'block' },
                  mt: 4,
                }}
                onError={(e) => {
                  const target = e.target as HTMLImageElement;
                  target.style.display = 'none';
                }}
              />
            </Box>
          </Box>

          {/* Right side - Login Form */}
          <Box
            sx={{
              width: '100%',
              maxWidth: 480,
              position: 'relative',
              zIndex: 2,
            }}
          >
            <Paper
              elevation={isMobile ? 0 : 8}
              sx={{
                p: { xs: 3, sm: 4 },
                borderRadius: 4,
                background: 'rgba(255, 255, 255, 0.9)',
                backdropFilter: 'blur(10px)',
                border: '1px solid rgba(255, 255, 255, 0.2)',
                boxShadow: '0 10px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
              }}
            >
              <Box sx={{ textAlign: 'center', mb: 4 }}>
                <Typography 
                  variant="h4" 
                  component="h2"
                  sx={{
                    fontWeight: 700,
                    mb: 1,
                    color: 'primary.main',
                  }}
                >
                  Sign In
                </Typography>
                <Typography 
                  variant="body2" 
                  color="text.secondary"
                  sx={{
                    opacity: 0.8,
                  }}
                >
                  Enter your credentials to continue
                </Typography>
              </Box>
              
              <LoginForm onSuccess={handleLoginSuccess} />
              
              <Box mt={4} textAlign="center">
                <Typography 
                  variant="body2" 
                  color="text.secondary"
                  sx={{ 
                    opacity: 0.7,
                    fontSize: '0.8rem',
                  }}
                >
                  © {new Date().getFullYear()} Maof POS System. All rights reserved.
                </Typography>
              </Box>
            </Paper>
          </Box>
        </Container>
      </Box>
      <style>{`
        body {
          margin: 0;
          padding: 0;
          overflow-x: hidden;
        }
      `}</style>
    </ThemeProvider>
  );
};

export default LoginPage;
