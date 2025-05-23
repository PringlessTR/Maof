import React from 'react';
import { Box, Typography, Button, Container } from '@mui/material';
import { useNavigate } from 'react-router-dom';

const UnauthorizedPage: React.FC = () => {
  const navigate = useNavigate();

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
        <Typography variant="h1" color="primary" fontWeight="bold" gutterBottom>
          403
        </Typography>
        <Typography variant="h4" gutterBottom>
          Yetkiniz Bulunmuyor
        </Typography>
        <Typography variant="body1" color="textSecondary" paragraph>
          Bu sayfayı görüntülemek için gerekli izinlere sahip değilsiniz.
          Lütfen yöneticinizle iletişime geçin.
        </Typography>
        <Box mt={3} display="flex" gap={2}>
          <Button
            variant="outlined"
            color="primary"
            onClick={handleGoBack}
            sx={{ minWidth: 120 }}
          >
            Geri Dön
          </Button>
          <Button
            variant="contained"
            color="primary"
            onClick={handleGoHome}
            sx={{ minWidth: 120 }}
          >
            Ana Sayfa
          </Button>
        </Box>
      </Box>
    </Container>
  );
};

export default UnauthorizedPage;
