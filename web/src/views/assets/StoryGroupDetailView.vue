<script setup lang="ts">
import type { Story, StoryGroup, StoryTypeInfo } from '@/api/assets'
import type { PagedResponse } from '@/api/types'
import { BookOpen, FolderOpen } from 'lucide-vue-next'
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { getStories, getStoryGroup, getStoryTypes } from '@/api/assets'
import { ApiError } from '@/api/client'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PaginationControls from '@/components/PaginationControls.vue'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import CardHeader from '@/components/ui/CardHeader.vue'
import CardTitle from '@/components/ui/CardTitle.vue'
import { useAuth } from '@/lib/auth'
import { createStoryTypeLabeler, formatDateTime } from '@/lib/display'
import { defaultPageSize, readQueryNumber } from '@/lib/query'
import { recordRecentStoryGroup } from '@/lib/recentAssets'

const route = useRoute()
const router = useRouter()
const { state } = useAuth()
const storyTypes = ref<StoryTypeInfo[]>([])
const group = ref<StoryGroup | null>(null)
const stories = ref<PagedResponse<Story> | null>(null)
const loading = ref(true)
const error = ref<ApiError | null>(null)
const storyGroupId = computed(() => Number(route.params.storyGroupId))
const storyTypeLabel = computed(() => createStoryTypeLabeler(storyTypes.value))

onMounted(loadPage)
watch(() => route.fullPath, loadPage)

async function loadPage() {
  loading.value = true
  error.value = null

  try {
    const page = readQueryNumber(route.query.page, 1)
    const pageSize = readQueryNumber(route.query.page_size, defaultPageSize)
    const [types, groupDetail, storyPage] = await Promise.all([
      storyTypes.value.length ? Promise.resolve(storyTypes.value) : getStoryTypes(),
      getStoryGroup(storyGroupId.value),
      getStories({ storyGroupId: storyGroupId.value, page, pageSize }),
    ])
    storyTypes.value = types
    group.value = groupDetail
    stories.value = storyPage
    recordRecentStoryGroup(state.currentTenant?.id, groupDetail)
  }
  catch (loadError) {
    error.value = loadError instanceof ApiError
      ? loadError
      : new ApiError(0, '剧情集详情加载失败，请稍后重试。')
  }
  finally {
    loading.value = false
  }
}

function changePage(page: number) {
  router.push({
    path: `/assets/groups/${storyGroupId.value}`,
    query: {
      page: String(page),
      page_size: String(stories.value?.page.pageSize || defaultPageSize),
    },
  })
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center gap-2 text-sm text-muted-foreground">
      <RouterLink class="hover:text-foreground" to="/assets">
        资产目录
      </RouterLink>
      <span>/</span>
      <RouterLink class="hover:text-foreground" to="/assets/groups?page=1&page_size=20">
        剧情集
      </RouterLink>
      <span>/</span>
      <span>{{ group?.title || '剧情集详情' }}</span>
    </div>

    <LoadingState v-if="loading" label="加载剧情集详情" />
    <ErrorState v-else-if="error" :error="error" @retry="loadPage" />

    <template v-else-if="group && stories">
      <Card>
        <CardHeader>
          <div class="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            <FolderOpen :size="18" class="text-primary" />
            <span>{{ storyTypeLabel(group.storyType) }}</span>
            <span>·</span>
            <span>编号 {{ group.displayNo ?? '-' }}</span>
          </div>
          <CardTitle class="text-2xl">
            {{ group.title }}
          </CardTitle>
        </CardHeader>
        <CardContent class="grid gap-4 text-sm md:grid-cols-2 xl:grid-cols-4">
          <div>
            <p class="text-muted-foreground">
              副标题
            </p>
            <p class="mt-1 font-medium">
              {{ group.subtitle || '-' }}
            </p>
          </div>
          <div>
            <p class="text-muted-foreground">
              外部类型
            </p>
            <p class="mt-1 font-medium">
              {{ group.externalType || '-' }}
            </p>
          </div>
          <div>
            <p class="text-muted-foreground">
              外部 ID
            </p>
            <p class="mt-1 font-medium">
              {{ group.externalId || '-' }}
            </p>
          </div>
          <div>
            <p class="text-muted-foreground">
              更新时间
            </p>
            <p class="mt-1 font-medium">
              {{ formatDateTime(group.updatedAt) }}
            </p>
          </div>
        </CardContent>
      </Card>

      <section class="space-y-3">
        <div class="flex items-center justify-between gap-3">
          <h2 class="text-base font-semibold">
            剧情列表
          </h2>
          <RouterLink
            class="text-sm text-primary hover:underline"
            :to="{ path: '/stories', query: { story_group_id: group.id, page: '1', page_size: '20' } }"
          >
            打开全局列表
          </RouterLink>
        </div>
        <EmptyState
          v-if="stories.items.length === 0"
          title="该剧情集暂无剧情"
          description="同步任务完成后，剧情会出现在这里。"
        />
        <Card v-else>
          <CardContent class="p-0">
            <RouterLink
              v-for="story in stories.items"
              :key="story.id"
              :to="`/stories/${story.id}`"
              class="flex flex-col gap-3 border-b p-4 transition last:border-b-0 hover:bg-accent/60 md:flex-row md:items-center md:justify-between"
            >
              <div class="min-w-0">
                <div class="flex items-center gap-2">
                  <BookOpen :size="18" class="text-primary" />
                  <p class="font-medium">
                    {{ story.title }}
                  </p>
                </div>
                <p class="mt-2 text-xs text-muted-foreground">
                  scenario_id：{{ story.scenarioId }} · 排序 {{ story.sortOrder }}
                </p>
              </div>
              <p class="shrink-0 text-xs text-muted-foreground">
                更新于 {{ formatDateTime(story.updatedAt) }}
              </p>
            </RouterLink>
          </CardContent>
          <div class="p-4">
            <PaginationControls :page="stories.page" @change="changePage" />
          </div>
        </Card>
      </section>
    </template>
  </div>
</template>
