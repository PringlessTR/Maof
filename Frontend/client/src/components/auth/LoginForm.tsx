import React from 'react';
import { useFormik } from 'formik';
import * as Yup from 'yup';
import { useNavigate, useLocation } from 'react-router-dom';
import {
  TextField,
  Button,
  Box,
  Typography,
  Alert,
  CircularProgress,
  InputAdornment,
  IconButton,
  FormControlLabel,
  Checkbox,
  Divider,
  useTheme,
  useMediaQuery,
} from '@mui/material';
import { Visibility, VisibilityOff, Login as LoginIcon } from '@mui/icons-material';
import { useAuth } from '../../context/AuthContext';

const validationSchema = Yup.object({
  username: Yup.string().required('Username is required'),
  password: Yup.string().required('Password is required'),
});

interface LoginFormProps {
  onSuccess?: () => void;
}

const LoginForm: React.FC<LoginFormProps> = ({ onSuccess }) => {
  const [error, setError] = React.useState<string>('');
  const [loading, setLoading] = React.useState<boolean>(false);
  const [showPassword, setShowPassword] = React.useState<boolean>(false);
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const from = location.state?.from?.pathname || '/';

  const formik = useFormik({
    initialValues: {
      username: '',
      password: ''
    },
    validationSchema,
    onSubmit: async (values) => {
      setError('');
      setLoading(true);
      try {
        setLoading(true);
        setError('');
        
        // Call login with username and password
        await login(values.username, values.password);
        
        // Call onSuccess callback if provided
        if (onSuccess) {
          onSuccess();
        }
        
        // Navigate to the requested URL or home page
        navigate(from, { replace: true });
      } catch (err) {
        console.error('Login error:', err);
        const errorMessage = err instanceof Error ? 
          err.message : 'Login failed. Please check your credentials and try again.';
        setError(errorMessage);
      } finally {
        setLoading(false);
      }
    },
  });

  const togglePasswordVisibility = () => {
    setShowPassword(!showPassword);
  };

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));

  return (
    <Box>
      {error && (
        <Alert 
          severity="error" 
          sx={{ 
            mb: 3,
            borderRadius: 2,
            '& .MuiAlert-message': {
              width: '100%',
            },
          }}
        >
          {error}
        </Alert>
      )}

      <form onSubmit={formik.handleSubmit}>
        <Box sx={{ mb: 3 }}>
          <TextField
            fullWidth
            id="username"
            name="username"
            label="Username"
            placeholder="Enter your username"
            value={formik.values.username}
            onChange={formik.handleChange}
            onBlur={formik.handleBlur}
            error={formik.touched.username && Boolean(formik.errors.username)}
            helperText={formik.touched.username && formik.errors.username}
            margin="normal"
            disabled={loading}
            autoComplete="username"
            autoFocus
            variant="outlined"
            InputProps={{
              style: {
                borderRadius: 12,
                fontSize: '0.95rem',
              },
            }}
            InputLabelProps={{
              style: {
                fontSize: '0.95rem',
              },
            }}
          />
        </Box>


        <Box sx={{ mb: 1 }}>
          <TextField
            fullWidth
            id="password"
            name="password"
            label="Password"
            type={showPassword ? 'text' : 'password'}
            placeholder="Enter your password"
            value={formik.values.password}
            onChange={formik.handleChange}
            onBlur={formik.handleBlur}
            error={formik.touched.password && Boolean(formik.errors.password)}
            helperText={formik.touched.password && formik.errors.password}
            margin="normal"
            disabled={loading}
            autoComplete="current-password"
            variant="outlined"
            InputProps={{
              style: {
                borderRadius: 12,
                fontSize: '0.95rem',
              },
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton
                    aria-label="toggle password visibility"
                    onClick={togglePasswordVisibility}
                    edge="end"
                    size="small"
                    sx={{
                      color: 'text.secondary',
                      '&:hover': {
                        backgroundColor: 'rgba(0, 0, 0, 0.02)',
                      },
                    }}
                  >
                    {showPassword ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                  </IconButton>
                </InputAdornment>
              ),
            }}
            InputLabelProps={{
              style: {
                fontSize: '0.95rem',
              },
            }}
          />
        </Box>



        <Button
          type="submit"
          fullWidth
          variant="contained"
          color="primary"
          disabled={loading}
          size="large"
          startIcon={!loading && <LoginIcon />}
          sx={{
            py: 1.5,
            borderRadius: 3,
            textTransform: 'none',
            fontSize: '1rem',
            fontWeight: 500,
            boxShadow: '0 4px 14px 0 rgba(124, 77, 255, 0.3)',
            '&:hover': {
              boxShadow: '0 6px 20px 0 rgba(124, 77, 255, 0.4)',
            },
            mt: 1,
          }}
        >
          {loading ? (
            <CircularProgress size={24} color="inherit" />
          ) : (
            'Sign In'
          )}
        </Button>
      </form>

      <Divider sx={{ my: 3, color: 'text.secondary', fontSize: '0.8rem' }}>
        Secure Login
      </Divider>
    </Box>
  );
};

export default LoginForm;
