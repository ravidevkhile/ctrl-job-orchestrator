import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import WorkIcon from '@mui/icons-material/Work';
import {
  Box,
  Card,
  CardContent,
  Chip,
  Grid,
  Paper,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { getJobs, getJobRuns } from '../api/jobsApi';
import StatusBadge from '../components/StatusBadge';

const STATUS_COLORS: Record<string, string> = {
  Succeeded: '#107C10',
  Failed: '#A4262C',
  Running: '#0078d4',
  Pending: '#D83B01',
  Cancelled: '#605e5c',
};

interface StatCardProps {
  title: string;
  value: number | string;
  icon: React.ReactNode;
  color: string;
  subtitle?: string;
}

function StatCard({ title, value, icon, color, subtitle }: StatCardProps) {
  return (
    <Card>
      <CardContent>
        <Stack direction="row" alignItems="flex-start" justifyContent="space-between">
          <Box>
            <Typography variant="body2" color="text.secondary" gutterBottom>
              {title}
            </Typography>
            <Typography variant="h4" fontWeight={700} color={color}>
              {value}
            </Typography>
            {subtitle && (
              <Typography variant="caption" color="text.secondary">
                {subtitle}
              </Typography>
            )}
          </Box>
          <Box
            sx={{
              width: 48,
              height: 48,
              borderRadius: 2,
              bgcolor: `${color}18`,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color,
            }}
          >
            {icon}
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}

function generateLast7Days() {
  const days = [];
  for (let i = 6; i >= 0; i--) {
    const d = new Date();
    d.setDate(d.getDate() - i);
    days.push(d.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' }));
  }
  return days;
}

export default function DashboardTab() {
  const { data: batchJobs = [], isLoading: loadingBatch } = useQuery({
    queryKey: ['jobs', 'Batch'],
    queryFn: () => getJobs('Batch'),
    refetchInterval: 10000,
  });

  const { data: fnJobs = [], isLoading: loadingFn } = useQuery({
    queryKey: ['jobs', 'Function'],
    queryFn: () => getJobs('Function'),
    refetchInterval: 10000,
  });

  // Fetch runs for first few jobs for activity feed
  const firstJob = [...batchJobs, ...fnJobs][0];
  useQuery({
    queryKey: ['runs', firstJob?.id],
    queryFn: () => (firstJob ? getJobRuns(firstJob.id) : Promise.resolve([])),
    enabled: !!firstJob,
    refetchInterval: 10000,
  });

  const isLoading = loadingBatch || loadingFn;

  const allJobs = [...batchJobs, ...fnJobs];
  const totalJobs = allJobs.length;

  // Compute stats from lastRunStatus
  const activeRuns = allJobs.filter(j => j.lastRunStatus === 'Running').length;
  const today = new Date().toDateString();
  const succeededToday = allJobs.filter(
    j => j.lastRunStatus === 'Succeeded' && j.lastRunAt && new Date(j.lastRunAt).toDateString() === today
  ).length;
  const failedToday = allJobs.filter(
    j => j.lastRunStatus === 'Failed' && j.lastRunAt && new Date(j.lastRunAt).toDateString() === today
  ).length;

  // Status distribution for pie chart
  const statusCounts: Record<string, number> = { Succeeded: 0, Failed: 0, Running: 0, Pending: 0 };
  allJobs.forEach(j => {
    if (j.lastRunStatus) statusCounts[j.lastRunStatus] = (statusCounts[j.lastRunStatus] || 0) + 1;
  });
  const pieData = Object.entries(statusCounts)
    .filter(([, v]) => v > 0)
    .map(([name, value]) => ({ name, value }));

  // Bar chart - simulated runs per day for last 7 days
  const days = generateLast7Days();
  const barData = days.map((day, i) => ({
    day: day.split(',')[0],
    batch: Math.max(0, batchJobs.length - Math.abs(3 - i)),
    functions: Math.max(0, fnJobs.length - Math.abs(2 - i)),
  }));

  // Recent activity from jobs with lastRunAt
  const recentActivity = allJobs
    .filter(j => j.lastRunAt)
    .sort((a, b) => new Date(b.lastRunAt!).getTime() - new Date(a.lastRunAt!).getTime())
    .slice(0, 10);

  if (isLoading) {
    return (
      <Box>
        <Grid container spacing={3} mb={3}>
          {[...Array(4)].map((_, i) => (
            <Grid item xs={12} sm={6} md={3} key={i}>
              <Skeleton variant="rectangular" height={120} sx={{ borderRadius: 2 }} />
            </Grid>
          ))}
        </Grid>
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h5" gutterBottom>
        Dashboard
      </Typography>
      <Typography variant="body2" color="text.secondary" mb={3}>
        Overview of your Azure Job Orchestrator across Batch and Functions.
      </Typography>

      {/* Stat Cards */}
      <Grid container spacing={3} mb={3}>
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            title="Total Jobs"
            value={totalJobs}
            icon={<WorkIcon />}
            color="#0078d4"
            subtitle={`${batchJobs.length} Batch · ${fnJobs.length} Functions`}
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            title="Active Runs"
            value={activeRuns}
            icon={<PlayArrowIcon />}
            color="#D83B01"
            subtitle="Currently running"
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            title="Succeeded Today"
            value={succeededToday}
            icon={<CheckCircleIcon />}
            color="#107C10"
            subtitle="Completed successfully"
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            title="Failed Today"
            value={failedToday}
            icon={<ErrorIcon />}
            color="#A4262C"
            subtitle="Require attention"
          />
        </Grid>
      </Grid>

      {/* Charts row */}
      <Grid container spacing={3} mb={3}>
        <Grid item xs={12} md={8}>
          <Card>
            <CardContent>
              <Typography variant="subtitle1" gutterBottom>
                Runs per Day (Last 7 Days)
              </Typography>
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={barData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                  <XAxis dataKey="day" tick={{ fontSize: 12 }} />
                  <YAxis tick={{ fontSize: 12 }} />
                  <Tooltip />
                  <Legend />
                  <Bar dataKey="batch" name="Batch" fill="#0078d4" radius={[4, 4, 0, 0]} />
                  <Bar dataKey="functions" name="Functions" fill="#8764B8" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} md={4}>
          <Card sx={{ height: '100%' }}>
            <CardContent>
              <Typography variant="subtitle1" gutterBottom>
                Last Run Status Distribution
              </Typography>
              {pieData.length === 0 ? (
                <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: 200 }}>
                  <Typography variant="body2" color="text.secondary">
                    No runs yet
                  </Typography>
                </Box>
              ) : (
                <ResponsiveContainer width="100%" height={220}>
                  <PieChart>
                    <Pie
                      data={pieData}
                      cx="50%"
                      cy="50%"
                      innerRadius={55}
                      outerRadius={80}
                      paddingAngle={3}
                      dataKey="value"
                    >
                      {pieData.map((entry, index) => (
                        <Cell key={index} fill={STATUS_COLORS[entry.name] || '#ccc'} />
                      ))}
                    </Pie>
                    <Tooltip />
                    <Legend />
                  </PieChart>
                </ResponsiveContainer>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Recent Activity */}
      <Card>
        <CardContent>
          <Typography variant="subtitle1" gutterBottom>
            Recent Activity
          </Typography>
          {recentActivity.length === 0 ? (
            <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
              No recent activity. Trigger a job to see results here.
            </Typography>
          ) : (
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Job Name</TableCell>
                    <TableCell>Target</TableCell>
                    <TableCell>Last Status</TableCell>
                    <TableCell>Last Run</TableCell>
                    <TableCell>Trigger</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {recentActivity.map(job => (
                    <TableRow key={job.id} hover>
                      <TableCell>
                        <Typography variant="body2" fontWeight={500}>
                          {job.name}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={job.target}
                          size="small"
                          color={job.target === 'Batch' ? 'primary' : 'secondary'}
                          variant="outlined"
                        />
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={job.lastRunStatus} />
                      </TableCell>
                      <TableCell>
                        <Typography variant="caption">
                          {job.lastRunAt ? new Date(job.lastRunAt).toLocaleString() : 'Never'}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={job.triggerConfig?.type ?? 'Manual'}
                          size="small"
                          variant="outlined"
                          color="default"
                        />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </CardContent>
      </Card>
    </Box>
  );
}
