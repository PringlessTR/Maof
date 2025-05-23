import React from 'react';
import { Box, Typography, Button, Container } from '@mui/material';
import { useNavigate, useLocation } from 'react-router-dom';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';

const ForbiddenPage: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const state = location.state as { requiredPermission?: string };

  const handleGoBack = () => {
    navigate(-1);
  };

  const handleGoHome = () => {
    navigate('/');
  };

  return (
    <Container maxWidth="md">
      <Box
        display="flex"
        flexDirection="column"
        alignItems="center"
        justifyContent="center"
        minHeight="80vh"
        textAlign="center"
        p={3}
      >
        <Box
          width={80}
          height={80}
          borderRadius="50%"
          bgcolor="error.light"
          display="flex"
          alignItems="center"
          justifyContent="center"
          mb={3}
        >
          <LockOutlinedIcon fontSize="large" sx={{ color: 'white' }} />
        </Box>
        
        <Typography variant="h4" component="h1" gutterBottom fontWeight="bold">
          Erişim Engellendi
        </Typography>
        
        <Typography variant="body1" color="textSecondary" paragraph>
          Bu sayfayı görüntülemek için yeterli yetkiniz bulunmamaktadır.
        </Typography>
        
        {state?.requiredPermission && (
          <Box mt={2} mb={3} p={2} bgcolor="grey.100" borderRadius={1}>
            <Typography variant="body2" color="textSecondary">
              <strong>Gerekli İzin:</strong> {state.requiredPermission}
            </Typography>
          </Box>
        )}
        
        <Typography variant="body2" color="textSecondary" paragraph>
          Eğer bu bir hata olduğunu düşünüyorsanız, lütfen yöneticinizle iletişime geçin.
        </Typography>
        
        <Box mt={4} display="flex" gap={2} flexWrap="wrap" justifyContent="center">
          <Button
            variant="outlined"
            color="primary"
            onClick={handleGoBack}
            sx={{ minWidth: 160 }}
          >
            Önceki Sayfaya Dön
          </Button>
          <Button
            variant="contained"
            color="primary"
            onClick={handleGoHome}
            sx={{ minWidth: 160 }}
          >
            Ana Sayfaya Git
          </Button>
        </Box>
      </Box>
    </Container>
  );
};

export default ForbiddenPage;
